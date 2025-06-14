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
		private const float GIZMO_MODELS_SCALE_TO_REAL_GIZMO_SIZE = 0.075f;


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

					UpdateMatrices();
					DrawExtrusion(ref renderContext);
					DrawDrag3D();
					break;

				case BrushDrawGizmoMode.DrawStage.FinalizeShape:
					if(!AreAssetsLoaded())
					{
						break;
					}

					UpdateMatrices();
					DrawFaceControls(ref renderContext);
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

			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisForwards, BrushDrawGizmoMode.DragDirection.Forward,
				0.0f, GizmoMode.ExtrusionHeight * 0.5f, 0.0f, Mathf.Pi, 0.0f);
			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisBackwards, BrushDrawGizmoMode.DragDirection.Backward,
				0.0f, GizmoMode.ExtrusionHeight * -0.5f, 0.0f, 0.0f, 0.0f);
		}

		private void DrawFaceControls(ref RenderContext renderContext)
		{
			var startPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorStart);
			var endPoint = ProjectPointToPlane2D(GizmoMode.CursorPlane, GizmoMode.CursorEnd);
			var delta = endPoint - startPoint;
			delta.X = Mathr.Abs(delta.X);
			delta.Y = Mathr.Abs(delta.Y);

			Mesh transAxisMesh = _modelTranslationAxis.LODs[0].Meshes[0];

			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisForwards, BrushDrawGizmoMode.DragDirection.Upward,
				0.0f, Mathr.Abs(GizmoMode.ExtrusionHeight) * 0.5f, 0.0f, Mathf.Pi, 0.0f);
			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisForwards, BrushDrawGizmoMode.DragDirection.Downward,
				0.0f, Mathr.Abs(GizmoMode.ExtrusionHeight) * -0.5f, 0.0f, 0.0f, 0.0f);

			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisZ, BrushDrawGizmoMode.DragDirection.Forward,
				0.0f, 0.0f, delta.Y * -0.5f, Mathf.Pi * 0.5f, 0.0f);
			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisZ, BrushDrawGizmoMode.DragDirection.Backward,
				0.0f, 0.0f, delta.Y * 0.5f, Mathf.Pi * -0.5f, 0.0f);

			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisBackwards, BrushDrawGizmoMode.DragDirection.Rightward,
				delta.X * 0.5f, 0.0f, 0.0f, Mathf.Pi * 0.5f, Mathf.Pi * -0.5f);
			DrawMesh(ref renderContext, ref transAxisMesh, ref _materialAxisBackwards, BrushDrawGizmoMode.DragDirection.Leftward,
				delta.X * -0.5f, 0.0f, 0.0f, Mathf.Pi * -0.5f, Mathf.Pi * 0.5f);
		}

		private void DrawMesh(ref RenderContext renderContext, ref Mesh mesh, ref MaterialInstance material, BrushDrawGizmoMode.DragDirection dragDirection, 
			Real x, Real y, Real z, float pitch, float yaw)
		{
			if(GizmoMode.Dragging && GizmoMode.CurrentDragDirection != dragDirection)
				return;

			renderContext.View.GetWorldMatrix(ref _gizmoWorld, out Matrix worldMatrix);
			Matrix.Scaling(GIZMO_MODELS_SCALE_TO_REAL_GIZMO_SIZE, out Matrix scaleMatrix);

			//HACK: there is probably a way to do this with matrix calculations, but this works for now.
			var rotation = Quaternion.GetRotationFromTo(Vector3.Up, Vector3.Forward, Vector3.Up);
			var position = new Float3((float)x, (float)y, (float)z) * (_gizmoWorld.Orientation * rotation);

			Matrix.Translation(ref position, out Matrix m1);
			Matrix.Multiply(ref worldMatrix, ref m1, out m1);
			Matrix.Multiply(ref scaleMatrix, ref m1, out m1);

			Matrix.RotationX(pitch, out Matrix m2);
			Matrix.Multiply(ref m2, ref m1, out m1);
			Matrix.RotationY(yaw, out m2);
			Matrix.Multiply(ref m2, ref m1, out m1);

			mesh.Draw(ref renderContext, GizmoMode.CurrentDragDirection == dragDirection ? _materialAxisFocus : material, ref m1);
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