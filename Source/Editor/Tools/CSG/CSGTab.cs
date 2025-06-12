using System;
using FlaxEngine;
using FlaxEngine.GUI;
using FlaxEditor.GUI.Tabs;
using FlaxEditor.Viewport.Modes;

namespace FlaxEditor.Tools.CSG
{
	/// <summary>
    /// CSG tool tab. Create and edit CSG brushes.
    /// </summary>
    /// <seealso cref="GUI.Tabs.Tab" />
	[HideInEditor]
	public class CSGTab : Tab
	{
		private readonly Tabs _modes;

		/// <summary>
        /// The editor instance.
        /// </summary>
        public readonly Editor Editor;
		
		/// <summary>
		/// The brush drawing tab.
		/// </summary>
		public BrushDrawTab BrushDrawTab;

		/// <summary>
		/// Initialize a new instance of the <see cref="CSGTab"/> class.
		/// </summary>
		/// <param name="icon">The icon.</param>
		/// <param name="editor">The editor instance</param>
		public CSGTab(SpriteHandle icon, Editor editor)
			: base(string.Empty, icon)
		{
			Editor = editor;
			
			Selected += OnSelected;

			_modes = new Tabs
			{
				Orientation = Orientation.Horizontal,
				UseScroll = false,
				AnchorPreset = AnchorPresets.StretchAll,
				Offsets = Margin.Zero,
				TabsSize = new Float2(50, 32),
				Parent = this,
			};

			InitDrawMode();
			InitEditMode();
			InitUVMode();

			_modes.SelectedTabIndex = 0;
		}

		/// <inheritdoc />
        private void OnSelected(Tab tab)
        {
			UpdateGizmoMode();
			Editor.SceneEditing.Deselect();
        }

		/// <summary>
        /// Updates the active viewport gizmo mode based on the current mode.
        /// </summary>
		public void UpdateGizmoMode()
		{
			switch(_modes.SelectedTabIndex)
			{
				case 0:
					Editor.Windows.EditWin.Viewport.Gizmos.SetActiveMode<BrushDrawGizmoMode>();
					break;
				default:
					throw new IndexOutOfRangeException("Invalid CSG tab mode.");
			}
		}

		private void OnTabSelected(Tab tab)
		{
			UpdateGizmoMode();
		}

		private void InitDrawMode()
		{
			var tab = _modes.AddTab(BrushDrawTab = new BrushDrawTab(this, Editor.Windows.EditWin.Viewport.BrushDrawGizmo));
			tab.Selected += OnTabSelected;
		}

		private void InitEditMode()
		{
			
		}

		private void InitUVMode()
		{
			
		}
	}
}