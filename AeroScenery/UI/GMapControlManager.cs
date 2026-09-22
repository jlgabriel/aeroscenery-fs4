using AeroScenery.AFS2;
using GMap.NET.WindowsForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AeroScenery.UI
{
    public class GMapControlManager
    {
        public GMapControl GMapControl { get; set; }

        public GMapOverlay DrawGridSquare(AFS2GridSquare gridSquare, GridSquareDisplayType gridSquareDisplayType)
        {
            GMapPolygon polygon = new GMapPolygon(gridSquare.Coordinates, gridSquare.Name);

            switch (gridSquareDisplayType)
            {
                case GridSquareDisplayType.Selected:
                    polygon.Fill = new SolidBrush(Color.FromArgb(40, Color.Blue));
                    polygon.Stroke = new Pen(Color.Blue, 1);
                    break;
                case GridSquareDisplayType.Active:
                    polygon.Fill = new SolidBrush(Color.FromArgb(40, Color.Blue));
                    polygon.Stroke = new Pen(Color.DeepSkyBlue, 2);
                    break;
                //#MOD_k
                // Outline only, no fill. A translucent fill here is indistinguishable from the
                // translucent blue of a selected square once the two overlap, and "downloaded"
                // is not "selected". Tinted now means selected and nothing else.
                case GridSquareDisplayType.Downloaded:
                    polygon.Fill = new SolidBrush(Color.FromArgb(0, Color.Orange));
                    polygon.Stroke = new Pen(Color.Orange, 2);
                    break;
                //#MOD_g
                case GridSquareDisplayType.Show:
                    polygon.Fill = new SolidBrush(Color.FromArgb(40, Color.GhostWhite));
                    polygon.Stroke = new Pen(Color.GhostWhite, 1);
                    break;

                default:
                    break;
            }


            GMapOverlay polygonOverlay = new GMapOverlay(gridSquare.Name);
            polygonOverlay.Polygons.Add(polygon);
            this.GMapControl.Overlays.Add(polygonOverlay);

            this.GMapControl.Refresh();
            polygonOverlay.IsVisibile = false;
            polygonOverlay.IsVisibile = true;

            return polygonOverlay;
        }

    }
}
