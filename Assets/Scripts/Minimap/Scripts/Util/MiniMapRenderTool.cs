using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;


namespace System.MiniMap
{
    [RequireComponent(typeof(Camera)), ExecuteInEditMode]
    public class MiniMapRenderTool : MonoBehaviour
    {

        public int msaa = 1;
        public SystemMapRender.RendersDivisions renderDivisions = SystemMapRender.RendersDivisions.Single;
        public string[] Resolutions = new string[] { "4096", "2048", "1024", "512", "256" };
        public int CurrentResolution = 1;
        public bool backgroundTransparent = true;
        public bool previewBorders = true;
        public bool blackBorders = true;
        public SystemMiniMap miniMap;

        private void OnGUI()
        {
            if (previewBorders)
            {
                if (blackBorders) GUI.color = Color.black;
                Rect ScreenShotRect = new Rect(0, 0, Screen.height, Screen.height);
                Vector2 Center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 HalfSize = new Vector2(ScreenShotRect.height * 0.5f, ScreenShotRect.height * 0.5f);

                GUI.DrawTexture(new Rect(Center.x - HalfSize.x, Center.y - HalfSize.y, 2, ScreenShotRect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.DrawTexture(new Rect(Center.x + HalfSize.x, Center.y - HalfSize.y, 2, ScreenShotRect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.DrawTexture(new Rect(Center.x - HalfSize.x, (Center.y - HalfSize.y), ScreenShotRect.width, 2), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.DrawTexture(new Rect(Center.x - HalfSize.x, (Center.y + HalfSize.y), ScreenShotRect.width + 2, 2), Texture2D.whiteTexture, ScaleMode.StretchToFill);

                GUI.color = blackBorders ? new Color(0, 0, 0, 0.3f) : new Color(1, 1, 1, 0.3f);
                GUI.DrawTexture(new Rect(Center.x - 1, Center.y - HalfSize.y, 2, ScreenShotRect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.DrawTexture(new Rect(Center.x - HalfSize.x, Center.y - 1, ScreenShotRect.width, 2), Texture2D.whiteTexture, ScaleMode.StretchToFill);

                GUI.color = Color.white;
            }
        }

        public void SetMiniMap(SystemMiniMap mm)
        {
            miniMap = mm;
            CenterBounds();
        }

        public void CenterBounds()
        {
            Vector3 v = transform.position;
            v.x = miniMap.mapBounds.position.x;
            v.z = miniMap.mapBounds.position.z;
            transform.position = v;
        }
    }
}