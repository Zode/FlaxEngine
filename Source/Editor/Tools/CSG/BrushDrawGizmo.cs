using FlaxEditor.Gizmo;
using FlaxEngine;
using System;



#if USE_LARGE_WORLDS
using Real = System.Double;
using Mathr = FlaxEngine.Mathd;
#else
using Real = System.Single;
using Mathr = FlaxEngine.Mathf;
#endif

namespace FlaxEditor.Tools.CSG
{
	/// <summary>
	/// The brush drawing gizmo.
	/// </summary>
	public partial class BrushDrawGizmo : GizmoBase
	{
		/// <summary>
		/// The brush drawing gizmo mode.
		/// </summary>
		public readonly BrushDrawGizmoMode GizmoMode;

		private Plane _lockPlane;
		private Model _modelTranslationAxis;
		private MaterialInstance _materialAxisForwards; //Y axis
		private MaterialInstance _materialAxisBackwards; //X axis
		private MaterialInstance _materialAxisZ;
		private MaterialInstance _materialAxisFocus;
		
		/// <summary>
		/// Initialize a new instance of the <see cref="BrushDrawGizmo"/> class.
		/// </summary>
		/// <param name="owner">The owner.</param>
        /// <param name="mode">The mode.</param>
		public BrushDrawGizmo(IGizmoOwner owner, BrushDrawGizmoMode mode)
			: base(owner)
		{
			GizmoMode = mode;

			_modelTranslationAxis = FlaxEngine.Content.LoadAsyncInternal<Model>("Editor/Gizmo/TranslationAxis");
			_materialAxisForwards = FlaxEngine.Content.LoadAsyncInternal<MaterialInstance>("Editor/Gizmo/MaterialAxisY");
			_materialAxisBackwards = FlaxEngine.Content.LoadAsyncInternal<MaterialInstance>("Editor/Gizmo/MaterialAxisX");
			_materialAxisZ = FlaxEngine.Content.LoadAsyncInternal<MaterialInstance>("Editor/Gizmo/MaterialAxisZ");
			_materialAxisFocus = FlaxEngine.Content.LoadAsyncInternal<MaterialInstance>("Editor/Gizmo/MaterialAxisFocus");
			if(_modelTranslationAxis == null 
				|| _materialAxisForwards == null 
				|| _materialAxisBackwards == null
				|| _materialAxisZ == null
				|| _materialAxisFocus == null)
			{
				Platform.Fatal("Failed to load transform gizmo resources.");
			}
		}

		/// <inheritdoc />
        public override void OnDeactivated()
        {
			GizmoMode.ClearDrag();
			GizmoMode.ClearHeight();
			GizmoMode.CurrentDrawStage = BrushDrawGizmoMode.DrawStage.Drag2DShape;
            base.OnDeactivated();
        }

		private bool AreAssetsLoaded()
		{
			if(_modelTranslationAxis == null || !_modelTranslationAxis.IsLoaded)
				return false;

			if(_materialAxisForwards == null || !_materialAxisForwards.IsLoaded)
				return false;

			if(_materialAxisBackwards == null || !_materialAxisBackwards.IsLoaded)
				return false;

			if(_materialAxisZ == null || !_materialAxisZ.IsLoaded)
				return false;

			if(_materialAxisFocus == null || !_materialAxisFocus.IsLoaded)
				return false;

			return true;
		} 

		private void ConstructCSGBrush()
		{
			bool isSubtractive = GizmoMode.CurrentDragDirection == BrushDrawGizmoMode.DragDirection.Backward;
			//if this was a subtractive brush, fix too great precision by nudging brush "backwards" from draw plane if allowed
			if(GizmoMode.FixSubtractions && isSubtractive)
			{
				Real fixDistance = 1.0f;
				GizmoMode.CursorStart += GizmoMode.CursorPlane.Normal * fixDistance;  
				GizmoMode.CursorEnd += GizmoMode.CursorPlane.Normal * fixDistance;
				GizmoMode.SetHeight(GizmoMode.ExtrusionHeight - fixDistance);
			}

			var midPoint = GizmoMode.CursorStart + (GizmoMode.CursorEnd - GizmoMode.CursorStart) * 0.5f;
			midPoint += GizmoMode.CursorPlane.Normal * GizmoMode.ExtrusionHeight * 0.5f;
			var startPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorStart);
			var endPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorEnd);

			//rotate everything by 90deg so that extrusion (forward) from the cursorplane becomes upward for the final brush
			var rotation = Quaternion.GetRotationFromTo(Vector3.Up, Vector3.Forward, Vector3.Up);
			var orientation = Quaternion.FromDirection(GizmoMode.CursorPlane.Normal) * rotation;
			var size = new Vector3(endPoint.X - startPoint.X, endPoint.Y - startPoint.Y, GizmoMode.ExtrusionHeight) * rotation;
			size.X = Mathr.Abs(size.X);
			size.Y = Mathr.Abs(size.Y);
			size.Z = Mathr.Abs(size.Z);

			switch(GizmoMode.CurrentShape)
			{
				case BrushDrawGizmoMode.BrushShapes.Cube:
					var boxBrush = new BoxBrush()
					{
						Position = midPoint,
						Orientation = orientation,
						Size = size,
						Mode = isSubtractive ? BrushMode.Subtractive : BrushMode.Additive,
					};

					Editor.Instance.SceneEditing.Spawn(boxBrush);
					break;

				default:
					throw new NotImplementedException($"Unimplemented brush tool shape: {GizmoMode.CurrentShape}");
			}
		}

		private static Vector2 ProjectPointToPlane2D(Plane plane, Vector3 point)
		{
			var planePos = plane.Normal * plane.D;

			//closest point on plane
			var relative = point - planePos;
			Real dot = Vector3.Dot(relative.Normalized, plane.Normal);
			var projected = point + plane.Normal * relative.Length * (-dot);

			var quat = Quaternion.FromDirection(plane.Normal);
			var right = Vector3.Left * quat;
			var up = Vector3.Cross(right, plane.Normal);

			Real x = Vector3.Dot((projected - planePos).Normalized, right);
			Real y = Vector3.Dot((projected - planePos).Normalized, up);
			Real dist = (projected - planePos).Length;

			return new Vector2(x*dist, y*dist);
		}

		private static Vector3 ProjectPointFromPlane3D(Plane plane, Vector2 point)
		{
			var planePos = plane.Normal * plane.D;

			var quat = Quaternion.FromDirection(plane.Normal);
			var right = Vector3.Left * quat;
			var up = Vector3.Cross(right, plane.Normal);

			var unprojected = (right * point.X) + (up  * point.Y);
			
			return planePos + unprojected;
		}
	}
}