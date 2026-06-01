using System.Collections.Generic;

namespace JueMingZ.UI.Legacy.Framework
{
    public sealed class LegacyUiControlCollection
    {
        private readonly List<LegacyUiControl> _controls = new List<LegacyUiControl>();

        public int Count
        {
            get { return _controls.Count; }
        }

        public void Add(LegacyUiControl control)
        {
            if (control != null)
            {
                _controls.Add(control);
            }
        }

        public LegacyUiElement Draw(LegacyUiContext context)
        {
            LegacyUiElement hovered = null;
            for (var index = 0; index < _controls.Count; index++)
            {
                var element = _controls[index].Draw(context);
                if (element != null && context != null && context.IsElementHovered(element.Id, element.Rect))
                {
                    hovered = element;
                }
            }

            return context == null ? hovered : context.HoveredElement ?? hovered;
        }

        public void Clear()
        {
            _controls.Clear();
        }
    }
}
