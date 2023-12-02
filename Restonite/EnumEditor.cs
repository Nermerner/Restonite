using Elements.Core;
using FrooxEngine.UIX;
using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Elements.Assets.SegmentedBuilder;

namespace Restonite
{
    internal class StatueTypeEditor
    {
        private Text _textDrive;
        private Sync<int> _target;

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
            _target.Value = (int)EnumUtil.ShiftEnum((IConvertible)(StatueType)_target.Value, delta);
            _textDrive.Content.Value = $"{(StatueType)_target.Value}";
        }

        public void Setup(Slot slot, Sync<int> field)
        {
            _target = field;

            UIBuilder ui = new UIBuilder(slot);
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
    }
}
