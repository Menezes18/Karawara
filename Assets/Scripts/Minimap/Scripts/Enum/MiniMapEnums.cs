using System;

namespace System.MiniMap
{
    public enum ItemEffect
    {
        Pulsing,
        Fade,
        None,
    }

    [Serializable]
    public enum MiniMapFullScreenMode
    {
        
        NoFullScreen,

       
        ScreenArea,

       
        ScaleToCoverScreen,

       
        ScaleToFitScreen,
    }
}