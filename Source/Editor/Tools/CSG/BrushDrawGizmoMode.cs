using FlaxEditor.Gizmo;
using FlaxEditor.Viewport.Modes;
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
	/// <summary>
    /// Brush drawing mode.
    /// </summary>
    /// <seealso cref="FlaxEditor.Viewport.Modes.EditorGizmoMode" />
	public class BrushDrawGizmoMode : EditorGizmoMode
	{
		/// <summary>
		/// Possible brush shapes to draw and construct.
		/// </summary>
		public enum BrushShapes
		{
			/// <summary>
			/// A cube shape.
			/// </summary>
			Cube,
			/// <summary>
			/// A cylinder shape.
			/// </summary>
			Cylinder,
			/// <summary>
			/// A pyramid shape.
			/// </summary>
			Pyramid,
			/// <summary>
			/// A corner apex pyramid shape.
			/// </summary>
			CornerApexPyramid,
			/// <summary>
			/// A wedge shape.
			/// </summary>
			Wedge,
			/// <summary>
			/// A dodecahedron shape.
			/// </summary>
			Dodecahedron,
		}

		/// <summary>
		/// Current drawing stage of the CSG brush tool.
		/// </summary>
		public enum DrawStage
		{
			/// <summary>
			/// Initial stage, this is where the XZ size is drawn .
			/// </summary>
			Drag2DShape,
			/// <summary>
			/// Second stage, this is where the height is extruded.
			/// </summary>
			Extrude3DShape,
			/// <summary>
			/// Final stage, this is where quick adjustments can be made.
			/// </summary>
			FinalizeShape,
		}

		/// <summary>
		/// Extrusion drag direction.
		/// </summary>
		public enum DragDirection
		{
			/// <summary>
			/// No direction.
			/// </summary>
			None,
			/// <summary>
			/// Additive (forward) direction.
			/// </summary>
			Forward,
			/// <summary>
			/// Subtractive (backward) direction.
			/// </summary>
			Backward,
		}

		/// <summary>
		/// The brush drawing gizmo.
		/// </summary>
		public BrushDrawGizmo Gizmo;
		/// <summary>
		/// Is the current 3d cursor considered valid.
		/// </summary>
		public bool CursorValid {get; private set;} = false;
		/// <summary>
		/// The plane on which the 3d cursor is drawing upon.
		/// </summary>
		public Plane CursorPlane {get; private set;} = Plane.Default;
		/// <summary>
		/// The position of the 3d cursor.
		/// </summary>
		public Vector3 CursorPosition {get; private set;} = Vector3.Zero;
		/// <summary>
		/// Starting point of the brush shape.
		/// </summary>
		public Vector3 CursorStart = Vector3.Zero;
		/// <summary>
		/// Ending point of the brush shape.
		/// </summary>
		public Vector3 CursorEnd = Vector3.Zero;
		/// <summary>
		/// Is the tool currently dragging (includes extruding)?
		/// </summary>
		public bool Dragging {get; private set;} = false;
		/// <summary>
		/// The current drawing stage.
		/// </summary>
		public DrawStage CurrentDrawStage = DrawStage.Drag2DShape;
		/// <summary>
		/// The current extrusion directoion.
		/// </summary>
		public DragDirection CurrentDragDirection = DragDirection.None;
		/// <summary>
		/// The current extrusion height (may be a snapped value).
		/// </summary>
		public Real ExtrusionHeight {get; private set;} = 0.0f;
		private Real _extrusionHeight = 0.0f;
		

		private BrushShapes _currentShape = BrushShapes.Cube;
		/// <summary>
		/// Current shape to generate upon finishing extrusion.
		/// </summary>
		public BrushShapes CurrentShape
		{
			get => _currentShape;
			set
			{
				if(_currentShape != value)
				{
					_currentShape = value;
				}
			}
		}
		/// <summary>
		/// Should we draw the brush from the center or from a corner?
		/// </summary>
		public bool DrawFromCenter = false;
		/// <summary>
		/// Should we attempt to fix subtractions?
		/// </summary>
		public bool FixSubtractions = true;

		/// <inheritdoc />
		public override void Init(IGizmoOwner owner)
		{
			base.Init(owner);
			Gizmo = new BrushDrawGizmo(owner, this);	
		}

		/// <inheritdoc />
		public override void OnActivated()
		{
			base.OnActivated();
			Owner.Gizmos.Active = Gizmo;
		}

		/// <inheritdoc />
		public override void OnDeactivated()
		{
			base.OnDeactivated();
		}

		/// <summary>
		/// Clear the 3d cursor.
		/// </summary>
		public void ClearCursor()
		{
			CursorValid = false;
		}

		/// <summary>
		/// Set the 3d cursor.
		/// </summary>
		/// <param name="worldPos">World position of the curso.</param>
		/// <param name="plane">Cursor plane.</param>
		public void SetCursor(Vector3 worldPos, Plane plane)
		{
			if(Editor.Instance.MainTransformGizmo.TranslationSnapEnable || Owner.IsControlDown)
			{
				float snapValue = Editor.Instance.MainTransformGizmo.TranslationSnapValue;
				worldPos.X = Mathr.Round(worldPos.X / snapValue) * snapValue;
				worldPos.Y = Mathr.Round(worldPos.Y / snapValue) * snapValue;
				worldPos.Z = Mathr.Round(worldPos.Z / snapValue) * snapValue;
			}

			CursorValid = true;
			CursorPosition = worldPos;
			CursorPlane = plane;
		}

		/// <summary>
		/// Clear brush drag.
		/// </summary>
		public void ClearDrag()
		{
			Dragging = false;
			CurrentDrawStage = DrawStage.Drag2DShape;
		}

		/// <summary>
		/// Start brush drag.
		/// </summary>
		/// <returns>True if started drag, otherwise false.</returns>
		public bool StartDrag()
		{
			if(!CursorValid || Dragging)
				return false;
			
			Dragging = true;
			if(CurrentDrawStage == DrawStage.Drag2DShape)
			{
				CursorStart = CursorPosition;
			}

			return true;
		}

		/// <summary>
		/// End brush drag.
		/// </summary>
		/// <returns>True if ended drag, otherwise false.</returns>
		public bool EndDrag()
		{
			if(!Dragging)
				return false;
			
			Dragging = false;
			
			if(CurrentDrawStage == DrawStage.Drag2DShape)
			{
				CursorEnd = CursorPosition;
			}

			return true;
		}

		/// <summary>
		/// Clear extrusion height.
		/// </summary>
		public void ClearHeight()
		{
			_extrusionHeight = 0.0f;
			ExtrusionHeight = 0.0f;
		}

		/// <summary>
		/// Set new extrusion height from delta.
		/// </summary>
		/// <param name="delta">height delta.</param>
		public void SetHeightDelta(Real delta)
		{
			_extrusionHeight += delta;
			var tempHeight = _extrusionHeight;
			if(Editor.Instance.MainTransformGizmo.TranslationSnapEnable || Owner.IsControlDown)
			{
				Real snapValue = Editor.Instance.MainTransformGizmo.TranslationSnapValue;
				tempHeight = Mathr.Round(tempHeight / snapValue) * snapValue;
			}

			ExtrusionHeight = tempHeight;
		}

		/// <summary>
		/// Set new extrusion height.
		/// </summary>
		/// <param name="height">Extrusion height.</param>
		public void SetHeight(Real height)
		{
			_extrusionHeight = height;
			ExtrusionHeight = height;
		}
	}
}