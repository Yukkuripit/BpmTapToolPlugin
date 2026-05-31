using System;
using YukkuriMovieMaker.Plugin;

namespace BpmTapTool
{
    public class BpmTapTool : IToolPlugin
    {
        public string Name => "BPM打ち測定ツール";
        public Type ViewType => typeof(BpmTapToolView);
        public Type ViewModelType => typeof(BpmTapToolViewModel);

        public void Show() { }
    }
}