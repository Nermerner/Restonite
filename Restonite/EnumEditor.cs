using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using System;

namespace Restonite
{
    internal class StatueTypeEditor
    {
        #region Public Methods

        public void Setup(Slot slot, Sync<int> field)
        {
            _target = field;

            var ui = new UIBuilder(slot);
            RadiantUI_Constants.SetupEditorStyle(ui);

            ui.HorizontalLayout(4f);
            ui.Style.FlexibleWidth = -1f;
            ui.Style.MinWidth = 24f;
            LocaleString text = "<<";
            var decrement = ui.Button(in text);
            decrement.LocalPressed += DecrementEnum;
            ui.Style.FlexibleWidth = 100f;
            ui.Style.MinWidth = -1f;
            Button button = ui.Button();
            button.BaseColor.Value = RadiantUI_Constants.BUTTON_COLOR;
            _textDrive = button.Slot.GetComponentInChildren<Text>();
            _textDrive.Content.Value = $"{(StatueType)_target.Value}";
            ui.Style.FlexibleWidth = -1f;
            ui.Style.MinWidth = 24f;
            text = ">>";
            var increment = ui.Button(in text);
            increment.LocalPressed += IncrementEnum;
            ui.NestOut();
        }

        #endregion

        #region Private Fields

        private Sync<int>? _target;
        private Text? _textDrive;

        #endregion

        #region Private Methods

        private void DecrementEnum(IButton button, ButtonEventData eventData)
        {
            ShiftEnum(-1);
        }

        private void IncrementEnum(IButton button, ButtonEventData eventData)
        {
            ShiftEnum(1);
        }

        private void ShiftEnum(int delta)
        {
            if (_target is null || _textDrive is null) return;

            _target.Value = (int)EnumUtil.ShiftEnum((IConvertible)(StatueType)_target.Value, delta);
            _textDrive.Content.Value = $"{(StatueType)_target.Value}";
        }

        #endregion
    }
}
