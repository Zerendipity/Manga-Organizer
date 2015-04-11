using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SCA = SharpCompress.Archive;

namespace Nagru___Manga_Organizer
{
  public partial class Browse_Img : Form, IDisposable
	{
		#region Properties
    const int MAX_ERROR = 10;

		public Dictionary<int, int> Sort = new Dictionary<int, int>();
    bool bWideL, bWideR, bNext, bValidFocusLost = false;
    float fWidth;

    Timer trFlip;
    bool bAuto = false;

		#region Interface
    public List<string> Files {
      get;
      set;
    }
    public SCA.IArchiveEntry[] Archive {
      get;
      set;
    }
    public int Page {
      get;
      set;
    }
		#endregion

		#endregion

		#region Initialization

    public Browse_Img()
    {
        InitializeComponent();
        Resize += Form1_Resize;
    }

    private void Form1_Resize(object sender, EventArgs e)
    {
        Reload();
    }
      

		private void Browse_Load(object sender, EventArgs e)
    {
      this.LostFocus += Browse_Img_LostFocus;
      this.Location = Main.ActiveForm.Location;

      //Compensate for zip files being improperly sorted
      Files.Sort(new TrueCompare());
      if (Archive != null) {
        for (int x = 0; x < Archive.Length; x++) {
          for (int y = 0; y < Archive.Length; y++) {
            if (Archive[x].FilePath.Equals(Files[y])) {
              Sort.Add(y, x);
              break;
            }
          }
        }
      }

      //set up timer
      trFlip = new Timer();
      trFlip.Tick += trFlip_Tick;
      trFlip.Interval = Int32.Parse(SQL.GetSetting(SQL.Setting.ReadInterval));

      #if !DEBUG
      Cursor.Hide();
      Bounds = Screen.GetWorkingArea(this.picBx);
      FormBorderStyle = FormBorderStyle.None;
      WindowState = FormWindowState.Maximized;
      #endif

      picBx.BackColor = Color.FromArgb(Int32.Parse(SQL.GetSetting(SQL.Setting.BackgroundColour)));
      fWidth = (float)(Bounds.Width / 2.0);

    }

    private void Browse_Img_LostFocus(object sender, EventArgs e)
    {
      if (!bValidFocusLost) {
        this.Close();
      }
      else {
        bValidFocusLost = false;
      }
    }

    private void Browse_Shown(object sender, EventArgs e)
    {
			//if the browser is being opened to the last viewed page,
			//use the Prev() function to display those pages
      if (Page == -1) {
        Next();
      }
      else {
        Prev();
      }
    }

		#endregion

		#region Events

    private void Browse_KeyDown(object sender, KeyEventArgs e)
    {
			//handle changing timer interval settings
      if (bAuto && e.Modifiers == Keys.Shift) {
        if (e.KeyCode == Keys.Oemplus) {
          bAuto = true;
          Console.Beep(700, 100);
          trFlip.Interval += 500;
          Tmr_Reset();
        }
        else if (e.KeyCode == Keys.OemMinus) {
          if (trFlip.Interval >= 1500) {
            bAuto = true;
            Console.Beep(100, 100);
            trFlip.Interval -= 500;
            Tmr_Reset();
          }
        }
        return;
      }

      switch (e.KeyCode) {
        #region Traversal
        case Keys.Left:
        case Keys.A:
          Next();
          break;
        case Keys.Right:
        case Keys.D:
          Prev();
          break;
        case Keys.Up:
          if ((Page += 7) > Files.Count)
            Page = Page - Files.Count;
          Next();
          break;
        case Keys.Down:
          if ((Page -= 7) < 0)
            Page = Files.Count + Page;
          Prev();
          break;
        case Keys.Home:
          Page = -1;
          Next();
          break;
        case Keys.End:
          Page = Files.Count + 1;
          Prev();
          break;
        #endregion
        #region Special Functions
        case Keys.F:
          if (bAuto)
            trFlip.Stop();
          Cursor.Show();
          BrowseTo fmGoTo = new BrowseTo(this);
          fmGoTo.iPage = (bWideR || bWideL) ? Page : Page - 1;
          bValidFocusLost = true;

          if (fmGoTo.ShowDialog() == DialogResult.OK) {
            Page = fmGoTo.iPage - 1;
            bNext = true;
            Next();
          }

          fmGoTo.Dispose();
          if (bAuto)
            trFlip.Start();
          this.Select();
          Cursor.Hide();
          break;
        case Keys.S:
          bAuto = !bAuto;
          if (bAuto) {
            Console.Beep(500, 100);
            trFlip.Start();
          }
          else {
            Console.Beep(300, 100);
            trFlip.Stop();
          }
          break;
        #endregion
        #region Ignored Keys
        case Keys.PrintScreen:
        case Keys.MediaNextTrack:
        case Keys.MediaPreviousTrack:
        case Keys.MediaPlayPause:
        case Keys.MediaStop:
        case Keys.VolumeUp:
        case Keys.VolumeDown:
        case Keys.VolumeMute:
        case Keys.LWin:
        case Keys.RWin:
          break;
        #endregion
        default:
          Close();
          break;
      }
    }

    private void Tmr_Reset()
    {
      trFlip.Stop();
      trFlip.Start();
    }

    void trFlip_Tick(object sender, EventArgs e)
    {
      Next();
    }

    private void Browse_MouseUp(object sender, MouseEventArgs e)
    {
      this.Close();
    }

    private void Browse_FormClosing(object sender, FormClosingEventArgs e)
    {
      if (bAuto) {
        SQL.UpdateSetting(SQL.Setting.ReadInterval, trFlip.Interval);

        if(trFlip != null) {
          trFlip.Stop();
          trFlip.Dispose();
        }
      }

      Cursor.Show();
      Page += 2;
    }

		#endregion

		#region Custom Methods

		/// <summary>
		/// Displays the next two pages
		/// </summary>
    private void Next()
    {
      Image imgL = null, imgR = null;		//holds the loaded image files
      byte byAttempt = 0;								//holds the number of attempts to load an image
      Reset(Next:true);									//reset the page values to their default

			//get the next valid, right-hand image file in the sequence
      do {
        if (++Page >= Files.Count) {
          Page = 0;
        }
        imgR = TryLoad(Page);
      } while (imgR == null && ++byAttempt < MAX_ERROR);

			//if the image is not multi-page, then load the next valid, left-hand image in the sequence
      if (imgR == null || !(bWideR = imgR.Height < imgR.Width)) {
        byAttempt = 0;
        do {
          if (++Page >= Files.Count) {
            Page = 0;
          }
          imgL = TryLoad(Page);
        } while (imgL == null && ++byAttempt < MAX_ERROR);

				//if this image is multi-page, decrement the page value so the next page turn will catch it
        if (imgL != null && (bWideL = imgL.Height < imgL.Width)) {
          Page--;
        }
      }

      Refresh(imgL, imgR);
    }

		/// <summary>
		/// Displays the previous two pages
		/// </summary>
    private void Prev()
    {
			//If neither of the previous images were multi-page, decrement the page value to not overshoot pages
      if (Page != 0 && !(bWideR || bWideL))
        Page--;

			Image imgL = null, imgR = null;		//holds the loaded image files
      byte byAttempt = 0;								//holds the number of attempts to load an image
			Reset(Next:false);								//reset the page values to their default

			//get the next valid, left-hand image file in the sequence
      do {
        if (--Page < 0){
          Page = Files.Count - 1;
        }
        else if (Page >= Files.Count) {
          Page = 0;
        }
        imgL = TryLoad(Page);
      } while (imgL == null && ++byAttempt < MAX_ERROR);

			//if the image is not multi-page, then load the next valid, right-hand image in the sequence
      if (imgL == null || !(bWideL = imgL.Height < imgL.Width)) {
        byAttempt = 0;
        do {
          if (--Page < 0) {
            Page = Files.Count - 1;
          }
          imgR = TryLoad(Page);
        } while (imgR == null && ++byAttempt < MAX_ERROR);

				//sets if the right-hand image is multi-page or not
        bWideR = (imgR != null) ? imgR.Height < imgR.Width : false;
        Page++;
      }

      Refresh(imgL, imgR);
    }

    /// <summary>
    /// Reload the iamges
    /// </summary>
    private void Reload()
    {
        Image imgL = null, imgR = null;		//holds the loaded image files
        Reset(Next: false);								//reset the page values to their default

        imgL = TryLoad(Page);
        imgR = TryLoad(Page -1);

        bWideL = imgL.Height < imgL.Width;
        bWideR = (imgR != null) ? imgR.Height < imgR.Width : false;

        Refresh(imgL, imgR);
    }

		/// <summary>
		/// Try to load the image at the indicated index
		/// </summary>
		/// <param name="iPos">The file index</param>
    private Bitmap TryLoad(int iPos)
    {
      Bitmap bmpTmp = null;

      using (MemoryStream ms = new MemoryStream()) {
        try {
          if (Archive != null) {
					  Archive[Sort[iPos]].WriteTo(ms);
          }
          else {
            using (FileStream fs = new FileStream(Files[iPos], FileMode.Open)) {
              fs.CopyTo(ms);
            }
          }

          bmpTmp = new Bitmap(ms);
          

          if (bmpTmp.Width > bmpTmp.Height)
          {
              bmpTmp = Ext.ScaleImage(bmpTmp, Bounds.Width, Bounds.Height);
          }
          else
          {
              bmpTmp = Ext.ScaleImage(bmpTmp, Bounds.Width / 2, Bounds.Height);
          }
          
        
        } catch (Exception ex) {
          Console.WriteLine(ex.Message);
        }
      }

      return bmpTmp;
    }

		/// <summary>
		/// Reset the image parameters to default
		/// </summary>
		/// <param name="Next">Whether it was called from Next()</param>
    private void Reset(bool Next)
    {
      if (bAuto)
        Tmr_Reset();
      bWideL = false;
      bWideR = false;
      bNext = Next;
      GC.Collect(0);
    }

		/// <summary>
		/// Process which images to draw & how
		/// </summary>
		/// <param name="imgL"></param>
		/// <param name="imgR"></param>
    private void Refresh(Image imgL, Image imgR)
    {
      picBx.Refresh();

      picBx.SuspendLayout();
      using(Graphics g = picBx.CreateGraphics())
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;

				if (!bWideL && !bWideR) {
					DrawImage(g, imgL, true);
					DrawImage(g, imgR, false);
				}
				else if (bNext)
					DrawImage(g, imgR, false);
				else
					DrawImage(g, imgL, true);
			}
      picBx.ResumeLayout();

			if (imgL != null)
				imgL.Dispose();
			if (imgR != null)
				imgR.Dispose();
    }

		/// <summary>
		/// Draw the image to the screen
		/// </summary>
		/// <param name="g">A graphics reference to the picturebox</param>
		/// <param name="imgL">The image to draw</param>
		private void DrawImage(Graphics g, Image img, bool bLeft)
		{

            if (img != null)
			{

				g.DrawImage(
					img
                     , (bLeft ? bWideL : bWideR) ? (int)(Bounds.Width - img.Width) / 2 : Width / 2 + (bLeft ? -(img.Width + 5) : 5)
					, (int)(picBx.Height / 2.0 - img.Height / 2.0)
					, img.Width
					, img.Height
				);
			}
		}

		#endregion
  }
}
