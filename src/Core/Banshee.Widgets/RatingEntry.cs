using Gtk;
using Gdk;
using System;

namespace Banshee.Widgets
{
    public class RatingEntry : Gtk.EventBox
    {
        private static int max_rating = 5;
        private static int min_rating = 1;
        private static Pixbuf icon_rated;
        private static Pixbuf icon_blank;
        
        public object RatedObject;
        
        private int rating;
        private bool embedded;
        private int y_offset = 4, x_offset = 4;
        private Gdk.Pixbuf display_pixbuf;
        
        public event EventHandler Changed;
        
        public RatingEntry() : this(1) 
        {
        }
        
        public RatingEntry(int rating) : this(rating, false)
        {
        }

        public RatingEntry(int rating, bool embedded)
        {
            if(IconRated.Height != IconNotRated.Height || IconRated.Width != IconNotRated.Width) {
                throw new ArgumentException("Rating widget requires that rated and blank icons have the same height and width");
            }
            
            this.rating = rating;
            this.embedded = embedded;
            
            if(embedded) {
                y_offset = 0;
                x_offset = 0;
            }
            
            CanFocus = true;
            
            display_pixbuf = new Pixbuf(Gdk.Colorspace.Rgb, true, 8, Width, Height);
            display_pixbuf.Fill(0xffffff00);
            DrawRating(DisplayPixbuf, Value);
            
            EnsureStyle();
            ShowAll();
        }
        
        ~RatingEntry ()
        {
            display_pixbuf.Dispose();
            display_pixbuf = null;
            
            icon_rated = null;
            icon_blank = null;
        }

        protected override void OnSizeRequested(ref Gtk.Requisition requisition)
        {
            requisition.Width = Width + (2 * x_offset);
            requisition.Height = Height + (2 * y_offset);
            base.OnSizeRequested(ref requisition);
        }
        
        public static Pixbuf DrawRating(int val)
        {
            Pixbuf buf = new Pixbuf(Gdk.Colorspace.Rgb, true, 8, Width, Height);
            DrawRating(buf, val);
            return buf;
        }
        
        private static void DrawRating(Pixbuf pbuf, int val)
        {
            for(int i = 0; i < MaxRating; i++) {
                if(i <= val - MinRating) {
                    IconRated.CopyArea(0, 0, IconRated.Width, IconRated.Height, 
                        pbuf, i * IconRated.Width, 0);
                } else {
                    IconNotRated.CopyArea(0, 0, IconRated.Width, IconRated.Height,
                        pbuf, i * IconRated.Width, 0);
                }
            }
        }
        
        private int RatingFromPosition(double x)
        {
            return x < x_offset + 1 ? 0 : (int)Math.Max(0, Math.Min(((x - x_offset) 
                / (double)icon_rated.Width) + 1, MaxRating));
        }
        
        protected override bool OnExposeEvent(Gdk.EventExpose evnt)
        {
            if(evnt.Window != GdkWindow) {
                return true;
            }
            
            int y_mid = (Allocation.Height - Height) / 2;

            if(!embedded) {            
                Gtk.Style.PaintShadow(Style, GdkWindow, StateType.Normal, ShadowType.In,
                    evnt.Area, this, "entry", 0, y_mid - y_offset, Allocation.Width, 
                    Height + (y_offset * 2));
            }

            GdkWindow.DrawPixbuf(Style.BackgroundGC(StateType.Normal), 
                display_pixbuf, 0, 0, x_offset, y_mid, Width, Height, Gdk.RgbDither.None, 0, 0);

            return true;
        }

        protected override bool OnButtonPressEvent(Gdk.EventButton evnt)
        {
            if(evnt.Button != 1) {
                return false;
            }
            
            HasFocus = true;
            Value = RatingFromPosition(evnt.X);
            return true;
        }
        
        public bool HandleKeyPress(Gdk.EventKey evnt)
        {
            return this.OnKeyPressEvent(evnt);
        }
        
        protected override bool OnKeyPressEvent(Gdk.EventKey evnt)
        {
            switch(evnt.Key) {
                case Gdk.Key.Up:
                case Gdk.Key.Right:
                case Gdk.Key.plus:
                case Gdk.Key.equal:
                    Value++;
                    return true;
                
                case Gdk.Key.Down:
                case Gdk.Key.Left:
                case Gdk.Key.minus:
                    Value--;
                    return true;
            }
            
            if(evnt.KeyValue >= (48 + MinRating - 1) && evnt.KeyValue <= (48 + MaxRating) && evnt.KeyValue <= 59) {
                Value = (int)evnt.KeyValue - 48;
                return true;
            }
            
            return false;
        }
        
        protected override bool OnScrollEvent(EventScroll args)
        {
            return HandleScroll(args);
        }

        public bool HandleScroll(EventScroll args)
        {
            switch(args.Direction) {
                case Gdk.ScrollDirection.Up:
                case Gdk.ScrollDirection.Right:
                    Value++;
                    return true;
                
                case Gdk.ScrollDirection.Down:
                case Gdk.ScrollDirection.Left:
                    Value--;
                    return true;
            }
            
            return false;
        }
        
        protected override bool OnMotionNotifyEvent(Gdk.EventMotion evnt)
        {
            // TODO draw highlights onmouseover a rating? (and clear on leaveNotify)
            if((evnt.State & Gdk.ModifierType.Button1Mask) == 0) {
                return false;
            }
            
            Value = RatingFromPosition(evnt.X);
            return true;
        }

        protected virtual void OnChanged()
        {
            DrawRating(DisplayPixbuf, Value);
            QueueDraw();

            EventHandler handler = Changed;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        public void SetValueFromPosition(int x)
        {
            Value = RatingFromPosition(x);
        }

        public int Value {
            get { return rating; }
            set {
                if(rating != value && value >= min_rating - 1 && value <= max_rating) {
                    rating = value;
                    OnChanged();
                }
            }
        }
        
        public int XOffset {
            get { return x_offset; }
        }
        
        public int YOffset {
            get { return y_offset; }
        }
        
        public Pixbuf DisplayPixbuf {
            get { return display_pixbuf; }
        }
        
        public static int MaxRating {
            get { return max_rating; }
            set { max_rating = value; }
        }
        
        public static int MinRating {
            get { return min_rating; }
            set { min_rating = value; }
        }
        
        public static int NumLevels {
            get { return max_rating - min_rating + 1; }
        }
        
        public static Pixbuf IconRated {
            get {
                if(icon_rated == null) {
                    icon_rated = Gdk.Pixbuf.LoadFromResource("rating-rated.png");
                }
                
                return icon_rated;
            }
            
            set { icon_rated = value; }
        }
        
        public static Pixbuf IconNotRated {
            get {
                if(icon_blank == null) {
                    icon_blank = Gdk.Pixbuf.LoadFromResource("rating-unrated.png");
                }
                
                return icon_blank;
            }
            
            set { icon_blank = value; }
        }
        
        public static int Width {
            get { return IconRated.Width * NumLevels; }
        }
        
        public static int Height {
            get { return IconRated.Height; }
        }
    }
}
