using FlaxEditor.CustomEditors;
using FlaxEditor.GUI.Tabs;
using FlaxEngine;
using FlaxEngine.GUI;

namespace FlaxEditor.Tools.CSG
{
	/// <summary>
	/// CSG tool tab. Draw new brushes in the viewport.
	/// </summary>
	public class BrushDrawTab : Tab
	{
		/// <summary>
        /// The object for brush draw settings adjusting via Custom Editor.
        /// </summary>
		private sealed class ProxyObject
		{
			[HideInEditor]
			private readonly BrushDrawGizmoMode _mode;

			public ProxyObject(BrushDrawGizmoMode mode)
			{
				_mode = mode;
			}

			[EditorOrder(0), EditorDisplay("Shape"), Tooltip("Brush shape to use.")]
			public BrushDrawGizmoMode.BrushShapes BrushShape
			{
				get => _mode.CurrentShape;
				set => _mode.CurrentShape = value;
			}

			[EditorOrder(10), EditorDisplay("Shape"), Tooltip("Draw brush from center instead from a corner.")]
			public bool DrawFromCenter
			{
				get => _mode.DrawFromCenter;
				set => _mode.DrawFromCenter = value;
			}

			[EditorOrder(20), EditorDisplay("Shape"), Tooltip("Move the initial surface backwards a bit in order to avoid issues caused by too great precision.")]
			public bool FixSubtractions
			{
				get => _mode.FixSubtractions;
				set => _mode.FixSubtractions = value;
			}
		}

		private readonly ProxyObject _proxy;
		private readonly CustomEditorPresenter _presenter;
		private readonly CSGTab _tab;
		private readonly BrushDrawGizmoMode _gizmo;

		/// <summary>
		/// Initializes a new instance of the <see cref="BrushDrawTab"/> class.
		/// </summary>
		/// <param name="tab">The parent tab.</param>
		/// <param name="gizmo">The gizmo mode.</param>
		public BrushDrawTab(CSGTab tab, BrushDrawGizmoMode gizmo)
			: base("Create")
		{
			_tab = tab;
			_gizmo = gizmo;
			_proxy = new ProxyObject(gizmo);

			Panel panel = new Panel(ScrollBars.None)
			{
				AnchorPreset = AnchorPresets.StretchAll,
				Offsets = Margin.Zero,
				Parent = this,
			};

			CustomEditorPresenter presenter = new CustomEditorPresenter(null);
			presenter.Panel.Parent = panel;
			presenter.Select(_proxy);
			_presenter = presenter;
		}
	}
}