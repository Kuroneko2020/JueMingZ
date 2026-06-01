using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacyTabBarControl : LegacyUiControl
    {
        public string SelectedPageId { get; set; }

        public LegacyTabBarControl()
        {
            Kind = "tabbar";
            SelectedPageId = string.Empty;
        }

        public override LegacyUiElement Draw(LegacyUiContext context)
        {
            if (context == null || !Visible)
            {
                return null;
            }

            var hovered = (LegacyUiElement)null;
            for (var index = 0; index < LegacyTabBar.Tabs.Length; index++)
            {
                var tab = LegacyTabBar.Tabs[index];
                var rect = LegacyTabBar.GetTabRect(Bounds, index);
                var button = new LegacyButtonControl
                {
                    Id = "tab:" + tab.Id,
                    Label = tab.DisplayName,
                    IconId = LegacyTabBar.GetIconIdFromElementId(tab.Id),
                    Kind = "tab",
                    Bounds = rect,
                    Selected = tab.Id == SelectedPageId,
                    TextScale = 0.86f
                };
                var element = button.Draw(context);
                if (element != null && context.IsElementHovered(element.Id, rect))
                {
                    hovered = element;
                }
            }

            return hovered;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
        }
    }
}
