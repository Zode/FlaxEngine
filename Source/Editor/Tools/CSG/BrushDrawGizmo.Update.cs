using System;
using FlaxEditor.Gizmo;
using FlaxEditor.SceneGraph;
using FlaxEngine;

#if USE_LARGE_WORLDS
using Real = System.Double;
using Mathr = FlaxEngine.Mathd;
#else
using Real = System.Single;
using Mathr = FlaxEngine.Mathf;
#endif

namespace FlaxEditor.Tools.CSG
{
	public partial class BrushDrawGizmo : GizmoBase
	{
		private Transform _gizmoWorld = Transform.Identity;
		private Vector3 _lastExtrusionIntersectionPoint = Vector3.Zero;
		private bool _wasLeftMouseDown = false;
		private bool _hasExtruded = false;

		private const float AXIS_LENGTH = 3.75f;
		private const float AXIS_OFFSET = 0.6f;
		private const float AXIS_THICKNESS = 0.25f;
		private BoundingBox _axisBoxForward = new BoundingBox(new Vector3(-AXIS_THICKNESS), new Vector3(AXIS_THICKNESS)).MakeOffsetted(AXIS_OFFSET * Vector3.UnitZ).Merge(AXIS_LENGTH * Vector3.UnitZ);
		private BoundingBox _axisBoxBackward = new BoundingBox(new Vector3(-AXIS_THICKNESS), new Vector3(AXIS_THICKNESS)).MakeOffsetted(-AXIS_OFFSET * Vector3.UnitZ).Merge(-AXIS_LENGTH * Vector3.UnitZ);

		private void UpdateMatrices()
		{
			var origin = GizmoMode.CursorStart + (GizmoMode.CursorEnd - GizmoMode.CursorStart) * 0.5f;
			origin += GizmoMode.CursorPlane.Normal * GizmoMode.ExtrusionHeight;
			float screenScale = Editor.Instance.Options.Options.Visual.GizmoSize;
			if (Owner.Viewport.UseOrthographicProjection)
			{
				screenScale *= 50 * Owner.Viewport.OrthographicScale;
			}
			else
			{
				var length = Owner.ViewPosition - origin;
				screenScale = (float)(length.Length / 25.0f * screenScale);
			}

			_gizmoWorld = new Transform(origin, Quaternion.FromDirection(GizmoMode.CursorPlane.Normal), new Float3(screenScale));
		}

		private bool IsDragValid()
		{
			if(!GizmoMode.CursorValid)
				return false;

			var startPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorStart);
			var endPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorEnd);

			if(Mathr.NearEqual(Mathr.Abs(startPoint.X - endPoint.X), 0)
				|| Mathr.NearEqual(Mathr.Abs(startPoint.Y - endPoint.Y), 0))
				return false;

			if(GizmoMode.CurrentDrawStage == BrushDrawGizmoMode.DrawStage.FinalizeShape
				&& Mathr.NearEqual(GizmoMode.ExtrusionHeight, 0))
				return false;
			 
			return true;
		}

		/// <inheritdoc />
		public override void Update(float dt)
		{
			base.Update(dt);

			if(!IsActive || Owner.IsRightMouseButtonDown)
			{
				return;
			}

			if(_wasLeftMouseDown)
			{
				if(!Owner.IsLeftMouseButtonDown)
				{
					_wasLeftMouseDown = false;
				}
				
				return;
			}

			switch(GizmoMode.CurrentDrawStage)
			{
				case BrushDrawGizmoMode.DrawStage.Drag2DShape:
					Drag2DShape();
					break;

				case BrushDrawGizmoMode.DrawStage.Extrude3DShape:
					if(!AreAssetsLoaded())
					{
						break;
					}

					Extrude3DShape();
					break;

				case BrushDrawGizmoMode.DrawStage.FinalizeShape:
					//for now just clear it straight
					GizmoMode.ClearDrag();
					GizmoMode.ClearHeight();
					GizmoMode.CurrentDrawStage = BrushDrawGizmoMode.DrawStage.Drag2DShape;
					break;

				default:
					throw new NotImplementedException($"Unimplemented draw stage mode: {GizmoMode.CurrentDrawStage}");
			}

			if(!GizmoMode.Dragging && !IsDragValid())
			{
				GizmoMode.ClearDrag();
				GizmoMode.ClearHeight();
				GizmoMode.CurrentDrawStage = BrushDrawGizmoMode.DrawStage.Drag2DShape;
			}
		}
		
		private void Drag2DShape()
		{
			Ray ray = Owner.MouseRay;
			Ray view = new Ray(Owner.ViewPosition, Owner.ViewDirection.Normalized);
			var flags = SceneGraphNode.RayCastData.FlagTypes.SkipEditorPrimitives 
						| SceneGraphNode.RayCastData.FlagTypes.SkipTriggers 
						| SceneGraphNode.RayCastData.FlagTypes.SkipColliders;
			var hit = Editor.Instance.Scene.Root.RayCast(ref ray, ref view, out float closest, out Vector3 normal, flags);

			if(!GizmoMode.Dragging)
			{
				if(hit != null)
				{
					Vector3 point = ray.GetPoint(closest);
					_lockPlane = new Plane(point, normal);
				}
				else
				{
					_lockPlane = new Plane(Vector3.Up, 0.0f);
				}
			}

			if(_lockPlane.Intersects(ref ray, out Vector3 pos))
				GizmoMode.SetCursor(pos, _lockPlane);
			else
				GizmoMode.ClearCursor();

			if(Owner.IsLeftMouseButtonDown)
			{
				GizmoMode.StartDrag();
			}
			else
			{
				if(GizmoMode.EndDrag())
				{
					GizmoMode.ClearHeight();
					GizmoMode.CurrentDrawStage = BrushDrawGizmoMode.DrawStage.Extrude3DShape;
					if(GizmoMode.DrawFromCenter)
					{
						Vector3 delta = GizmoMode.CursorEnd - GizmoMode.CursorStart;
						GizmoMode.CursorStart -= delta;
					}

					UpdateMatrices();
				}
			}
		}

		private void Extrude3DShape()
		{
			Ray ray = Owner.MouseRay;

			if(Owner.IsLeftMouseButtonDown)
			{
				if(GizmoMode.CurrentDragDirection == BrushDrawGizmoMode.DragDirection.None)
				{
					_wasLeftMouseDown = true;
					GizmoMode.ClearDrag();
					return;
				}

				GizmoMode.StartDrag();
				ExtrudeDragHeight();
				UpdateMatrices();
				return;
			}

			GizmoMode.EndDrag();
			_lastExtrusionIntersectionPoint = Vector3.Zero;
			if(_hasExtruded)
			{
				GizmoMode.CurrentDrawStage = BrushDrawGizmoMode.DrawStage.FinalizeShape;
				ConstructCSGBrush();
				_hasExtruded = false;
			}

			//Transform into local space from the gizmo's (world) space
			Ray localRay;
			_gizmoWorld.WorldToLocalVector(ref ray.Direction, out localRay.Direction);
			_gizmoWorld.WorldToLocal(ref ray.Position, out localRay.Position);
			GizmoMode.CurrentDragDirection = BrushDrawGizmoMode.DragDirection.None;
			
			if(_axisBoxForward.Intersects(ref localRay, out Real intersection))
				GizmoMode.CurrentDragDirection = BrushDrawGizmoMode.DragDirection.Forward;
			else if(_axisBoxBackward.Intersects(ref localRay, out intersection))
				GizmoMode.CurrentDragDirection = BrushDrawGizmoMode.DragDirection.Backward;
		}

		private void ExtrudeDragHeight()
		{
			Ray ray = Owner.MouseRay;
			Matrix.RotationQuaternion(ref _gizmoWorld.Orientation, out var rotationMatrix);
			Matrix.Invert(ref rotationMatrix, out var invRotationMatrix);

			ray.Position = Vector3.Transform(ray.Position, invRotationMatrix);
			Vector3.TransformNormal(ref ray.Direction, ref invRotationMatrix, out ray.Direction);

			var midPoint = GizmoMode.CursorStart + (GizmoMode.CursorEnd - GizmoMode.CursorStart) * 0.5f;

			var planeYZ = new Plane(Vector3.Left, Vector3.Transform(midPoint, invRotationMatrix).X);
			var planeZX = new Plane(Vector3.Down, Vector3.Transform(midPoint, invRotationMatrix).Y);
			var direction = Vector3.Normalize(ray.Position - midPoint);
			Real planeDotYZ = Mathr.Abs(Vector3.Dot(planeYZ.Normal, direction));
			Real planeDotZX = Mathr.Abs(Vector3.Dot(planeZX.Normal, direction));
			Plane plane = planeDotZX > planeDotYZ ? planeZX : planeYZ;

			if(ray.Intersects(ref plane, out Real intersection))
			{
				var intersectionPoint = ray.Position + ray.Direction * intersection;
				if(_lastExtrusionIntersectionPoint != Vector3.Zero)
				{
					GizmoMode.SetHeightDelta((intersectionPoint - _lastExtrusionIntersectionPoint).Z);
					_hasExtruded = true;
				}

				_lastExtrusionIntersectionPoint = intersectionPoint;
			}
		}
	}
}