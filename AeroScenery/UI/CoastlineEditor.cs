using AeroScenery.AFS2;
using GMap.NET;
using GMap.NET.WindowsForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AeroScenery.UI
{
    /// <summary>
    /// Draws the coastline on the map by dragging, and shows the cut it produces.
    ///
    /// Freehand rather than click-a-vertex-at-a-time, because the margin makes precision
    /// irrelevant: with the cut 5.5 km offshore, half a kilometre of hand wobble moves it half a
    /// kilometre over open water. What the user needs is speed over 900 km of coast, and a drag
    /// gives that where clicking would not.
    ///
    /// Two things follow from the drag. The map's own drag has to be turned off while drawing,
    /// since it is the same gesture - so panning moves to the arrow keys and to the screen edge.
    /// And the raw stroke has to be thinned on release, because a drag emits a point every few
    /// pixels and the cut cannot resolve them.
    ///
    /// Points are held in lat/lon, so panning mid-stroke leaves what is already drawn where it
    /// belongs and the overlay simply redraws it in the new place.
    /// </summary>
    public class CoastlineEditor
    {
        /// <summary>Pixels the cursor must travel before another point is taken.</summary>
        private const int MinPixelStep = 4;

        /// <summary>How close to the edge starts the auto-pan, and how fast it goes.</summary>
        private const int EdgeBand = 45;
        private const int EdgeStep = 12;

        /// <summary>Arrow keys move by a fixed slice of the view rather than a fixed distance.</summary>
        private const double ArrowFraction = 0.25;

        /// <summary>
        /// Thinning tolerance in km, and deliberately NOT a fraction of the margin.
        ///
        /// It was a fifth of the margin, which meant drawing at 3 NM straightened every bay under
        /// 1.1 km - the pilot traced round a bay and watched their own line go straight. And it is
        /// worse than it looks: the thinning is permanent while the margin is not, so a number
        /// meant to be re-tunable afterwards was quietly deciding how much of the coast survived.
        /// Changing the margin later cannot bring back a headland the tolerance ate.
        ///
        /// 200 m keeps every inlet worth seeing on the map, sits well under the one vertex per
        /// kilometre the cut actually needs, and still takes a stroke from tens of thousands of
        /// points to a few hundred.
        /// </summary>
        private const double ToleranceKm = 0.2;

        /// <summary>
        /// The coarsest ground resolution worth drawing at, in metres per pixel.
        ///
        /// Zoomed out past this, the mouse lays points down further apart than the thinning would
        /// have kept, so the zoom is what limits the line rather than the tolerance - and no amount
        /// of care with the hand gets that detail back afterwards. Derived from the two constants
        /// rather than written down, because a number copied into the toolbar is a number that
        /// drifts.
        /// </summary>
        public static double CoarsestMetresPerPixel
        {
            get { return ToleranceKm * 1000.0 / MinPixelStep; }
        }

        private readonly GMapControl map;
        private readonly GMapOverlay overlay = new GMapOverlay("coastline");
        private readonly Timer edgeTimer = new Timer();

        private bool active;
        private bool drawing;
        private System.Drawing.Point lastPixel;
        private System.Drawing.Point cursorPixel;

        public CoastlineEditor(GMapControl map)
        {
            this.map = map;
            Line = new Coastline();

            map.Overlays.Add(overlay);
            map.MouseDown += OnMouseDown;
            map.MouseMove += OnMouseMove;
            map.MouseUp += OnMouseUp;

            edgeTimer.Interval = 30;
            edgeTimer.Tick += OnEdgeTick;
        }

        public Coastline Line { get; private set; }

        /// <summary>Raised when the line changes, so the form can update its labels.</summary>
        public event EventHandler Changed;

        public bool Active
        {
            get { return active; }
            set
            {
                if (active == value)
                {
                    return;
                }
                active = value;

                // The map's drag IS the drawing gesture, so one of them has to give.
                map.CanDragMap = !active;
                map.Cursor = active ? Cursors.Cross : Cursors.Default;

                if (!active)
                {
                    StopDrawing();
                }
                Redraw();
            }
        }

        public double MarginKm
        {
            get { return Line.MarginKm; }
            set
            {
                Line.MarginKm = value;
                Redraw();
                OnChanged();
            }
        }

        public LandSide Land
        {
            get { return Line.Land; }
            set
            {
                Line.Land = value;
                Redraw();
                OnChanged();
            }
        }

        // -----------------------------------------------------------------------------------
        // drawing
        // -----------------------------------------------------------------------------------

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (!active || e.Button != MouseButtons.Left)
            {
                return;
            }

            drawing = true;
            Line.BeginStroke();
            lastPixel = new System.Drawing.Point(e.X, e.Y);
            Take(e.X, e.Y);
            edgeTimer.Start();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            cursorPixel = new System.Drawing.Point(e.X, e.Y);
            if (!active || !drawing)
            {
                return;
            }

            int dx = e.X - lastPixel.X;
            int dy = e.Y - lastPixel.Y;
            if (dx * dx + dy * dy < MinPixelStep * MinPixelStep)
            {
                return;
            }

            lastPixel = cursorPixel;
            Take(e.X, e.Y);

            // Only the coast is redrawn while the button is down. The cut costs a distance test
            // per offset point against every segment, which is nothing on release and far too
            // much at mouse-move rates.
            DrawCoastOnly();
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (!active || !drawing)
            {
                return;
            }
            StopDrawing();
            Redraw();
            OnChanged();
        }

        private void StopDrawing()
        {
            edgeTimer.Stop();
            if (drawing)
            {
                drawing = false;
                Line.EndStroke(ToleranceKm);
            }
        }

        private void Take(int x, int y)
        {
            PointLatLng p = map.FromLocalToLatLng(x, y);
            Line.AddPoint(p.Lat, p.Lng);
        }

        /// <summary>
        /// Scrolls when the cursor reaches the edge mid-stroke, which is what makes a coast longer
        /// than the screen drawable without letting go of the button.
        /// </summary>
        private void OnEdgeTick(object sender, EventArgs e)
        {
            if (!drawing)
            {
                edgeTimer.Stop();
                return;
            }

            int dx = 0, dy = 0;
            if (cursorPixel.X < EdgeBand) dx = -EdgeStep;
            else if (cursorPixel.X > map.Width - EdgeBand) dx = EdgeStep;
            if (cursorPixel.Y < EdgeBand) dy = -EdgeStep;
            else if (cursorPixel.Y > map.Height - EdgeBand) dy = EdgeStep;

            if (dx == 0 && dy == 0)
            {
                return;
            }

            Pan(dx, dy);

            // The cursor has not moved but the world under it has, so the point it now sits over
            // is a new one and belongs on the line. Without this the stroke stops dead at the
            // edge and resumes wherever the pan happens to end.
            Take(cursorPixel.X, cursorPixel.Y);
            lastPixel = cursorPixel;
            DrawCoastOnly();
        }

        /// <summary>Moves the view by pixels. Used by the edge scroll and by the arrow keys.</summary>
        public void Pan(int dx, int dy)
        {
            map.Position = map.FromLocalToLatLng(map.Width / 2 + dx, map.Height / 2 + dy);
        }

        public void PanByArrow(Keys key)
        {
            int x = (int)(map.Width * ArrowFraction);
            int y = (int)(map.Height * ArrowFraction);

            switch (key)
            {
                case Keys.Left: Pan(-x, 0); break;
                case Keys.Right: Pan(x, 0); break;
                case Keys.Up: Pan(0, -y); break;
                case Keys.Down: Pan(0, y); break;
            }
        }

        public bool Undo()
        {
            StopDrawing();
            bool undone = Line.UndoStroke();
            if (undone)
            {
                Redraw();
                OnChanged();
            }
            return undone;
        }

        public void Clear()
        {
            StopDrawing();
            Line.Clear();
            Redraw();
            OnChanged();
        }

        // -----------------------------------------------------------------------------------
        // rendering
        // -----------------------------------------------------------------------------------

        private static List<PointLatLng> ToGMap(List<GeoPoint> pts)
        {
            var outPts = new List<PointLatLng>(pts.Count);
            foreach (var p in pts)
            {
                outPts.Add(new PointLatLng(p.Lat, p.Lon));
            }
            return outPts;
        }

        private void DrawCoastOnly()
        {
            overlay.Routes.Clear();
            AddRoute(Line.Points, "coast", Color.Yellow, 2);
            map.Refresh();
        }

        /// <summary>
        /// Both lines: what was drawn, and where photoscenery will actually stop.
        ///
        /// Showing the cut is the point of the whole editor. The user is not drawing the
        /// boundary, they are drawing the thing the boundary is derived from, so without seeing
        /// the derived line they are working blind - and the margin is a number they are meant to
        /// be able to change their mind about.
        /// </summary>
        public void Redraw()
        {
            overlay.Routes.Clear();
            overlay.IsVisibile = !Line.IsEmpty;

            if (!Line.IsEmpty)
            {
                AddRoute(Line.CutLine(), "cut", Color.OrangeRed, 2);
                AddRoute(Line.Points, "coast", Color.Yellow, 2);
            }
            map.Refresh();
        }

        private void AddRoute(List<GeoPoint> pts, string name, Color colour, int width)
        {
            if (pts.Count < 2)
            {
                return;
            }
            var route = new GMapRoute(ToGMap(pts), name);
            route.Stroke = new Pen(colour, width);
            overlay.Routes.Add(route);
        }

        private void OnChanged()
        {
            var h = Changed;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }

        // -----------------------------------------------------------------------------------
        // storage
        // -----------------------------------------------------------------------------------

        public void Save(string path)
        {
            StopDrawing();
            Line.Save(path);
        }

        public void Load(string path)
        {
            Line = Coastline.Load(path);
            Redraw();
            OnChanged();
        }
    }
}
