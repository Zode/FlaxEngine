using System;
using FlaxEditor.Gizmo;
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
		/// <inheritdoc />
		public override void Draw(ref RenderContext renderContext)
		{
			if(!IsActive)
				return;

			switch(GizmoMode.CurrentDrawStage)
			{
				case BrushDrawGizmoMode.DrawStage.Drag2DShape:
					DrawCursor();
					DrawDrag2D();
					break;

				case BrushDrawGizmoMode.DrawStage.Extrude3DShape:
					if(!AreAssetsLoaded())
					{
						break;
					}

					DrawExtrusion(ref renderContext);
					DrawDrag3D();
					break;

				case BrushDrawGizmoMode.DrawStage.FinalizeShape:
					DrawDrag3D();
					break;

				default:
					throw new NotImplementedException($"Unimplemented draw stage mode: {GizmoMode.CurrentDrawStage}");
			}
		}

		private void DrawCursor()
		{
			if(!GizmoMode.CursorValid || GizmoMode.CurrentDrawStage != BrushDrawGizmoMode.DrawStage.Drag2DShape)
				return;

			var quaternion = Quaternion.FromDirection(GizmoMode.CursorPlane.Normal);
			var right = Vector3.Right * quaternion;
			var up = Vector3.Cross(-right, GizmoMode.CursorPlane.Normal);
			DebugDraw.DrawLine(GizmoMode.CursorPosition, GizmoMode.CursorPosition + GizmoMode.CursorPlane.Normal * 100.0f, Color.Red, 0.0f, false);
		}

		private void DrawDrag2D()
		{
			if(!GizmoMode.Dragging)
				return;
			
			var startPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorStart);
			var endPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorPosition);
			if(GizmoMode.DrawFromCenter)
			{
				DrawRectangleOnPlane(GizmoMode.CursorStart, GizmoMode.CursorPlane.Normal, (endPoint - startPoint) * 2.0f, Color.Red);
				return;
			}
			
			var midPoint = GizmoMode.CursorStart + (GizmoMode.CursorPosition - GizmoMode.CursorStart) * 0.5f;
			DrawRectangleOnPlane(midPoint, GizmoMode.CursorPlane.Normal, endPoint - startPoint, Color.Red);
		}

		private void DrawDrag3D()
		{
			var startPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorStart);
			var endPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorEnd);
			var midPoint = GizmoMode.CursorStart + (GizmoMode.CursorEnd - GizmoMode.CursorStart) * 0.5f;
			DrawCube(midPoint, GizmoMode.CursorPlane.Normal, startPoint - endPoint, GizmoMode.ExtrusionHeight, Color.Yellow);
		}

		private void DrawExtrusion(ref RenderContext renderContext)
		{
			Mesh transAxisMesh = _modelTranslationAxis.LODs[0].Meshes[0];
			
			renderContext.View.GetWorldMatrix(ref _gizmoWorld, out Matrix world);
			const float gizmoModelsScale2RealGizmoSize = 0.075f;
			Matrix.Scaling(gizmoModelsScale2RealGizmoSize, out Matrix m3);
			Matrix.Multiply(ref m3, ref world, out Matrix m1);

			if(!GizmoMode.Dragging)
			{
				Matrix.RotationX(Mathr.Pi, out Matrix m2);
				Matrix.Multiply(ref m2, ref m1, out m3);
				transAxisMesh.Draw(ref renderContext,
					GizmoMode.CurrentDragDirection == BrushDrawGizmoMode.DragDirection.Forward ? _materialAxisFocus : _materialAxisForwards,
					ref m3);

				Matrix.RotationX(0.0f, out m2);
				Matrix.Multiply(ref m2, ref m1, out m3);
				transAxisMesh.Draw(ref renderContext,
					GizmoMode.CurrentDragDirection == BrushDrawGizmoMode.DragDirection.Backward ? _materialAxisFocus : _materialAxisBackwards,
					ref m3);

				return;
			}

			switch(GizmoMode.CurrentDragDirection)
			{
				case BrushDrawGizmoMode.DragDirection.Forward:
					Matrix.RotationX(Mathr.Pi, out Matrix m2);
					Matrix.Multiply(ref m2, ref m1, out m3);
					transAxisMesh.Draw(ref renderContext, _materialAxisFocus, ref m3);
					break;

				case BrushDrawGizmoMode.DragDirection.Backward:
					Matrix.RotationX(0.0f, out m2);
					Matrix.Multiply(ref m2, ref m1, out m3);
					transAxisMesh.Draw(ref renderContext, _materialAxisFocus, ref m3);
					break;
			}
		}

		private static void DrawRectangleOnPlane(Vector3 position, Vector3 normal, Vector2 extents, Color color)
		{
			var orientation =	Quaternion.FromDirection(normal);
			var right = Vector3.Right * extents.X * 0.5f * orientation;
			var up = Vector3.Up * extents.Y * 0.5f * orientation;

			Vector3 a = -right + up + position;
			Vector3 b = right + up + position;
			Vector3 c = right - up + position;
			Vector3 d = -right - up + position;

			DebugDraw.DrawLine(a, b, color, 0.0f, false);
			DebugDraw.DrawLine(b, c, color, 0.0f, false);
			DebugDraw.DrawLine(c, d, color, 0.0f, false);
			DebugDraw.DrawLine(d, a, color, 0.0f, false);
		}

		private static void DrawCube(Vector3 position, Vector3 normal, Vector2 extents, Real height, Color color)
		{
			var upperPosition = position + normal * height;
			DrawRectangleOnPlane(position, normal, extents, color);
			DrawRectangleOnPlane(upperPosition, normal, extents, color);

			var orientation =	Quaternion.FromDirection(normal);
			var right = Vector3.Right * extents.X * 0.5f * orientation;
			var up = Vector3.Up * extents.Y * 0.5f * orientation;

			Vector3 a = -right + up;
			Vector3 b = right + up;
			Vector3 c = right - up;
			Vector3 d = -right - up;

			DebugDraw.DrawLine(position + a, upperPosition + a, color, 0.0f, false);
			DebugDraw.DrawLine(position + b, upperPosition + b, color, 0.0f, false);
			DebugDraw.DrawLine(position + c, upperPosition + c, color, 0.0f, false);
			DebugDraw.DrawLine(position + d, upperPosition + d, color, 0.0f, false);
		}
	}
}