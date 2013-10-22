﻿/*
 * Author: Taylor Napier
 * Date: Aug15, 2012
 * Desc: Handles organization of manga library
 * 
 * This program is distributed under the
 * GNU General Public License v3 (GPLv3)
 * 
 * SharpCompress.dll is distributed under the 
 * MIT\Expat License (MIT)
 */

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.Serialization;
using SCA = SharpCompress.Archive;

namespace Nagru___Manga_Organizer
{
    public partial class Main : Form
    {
        delegate void DelVoid();
        delegate void DelInt(int iNum);
        delegate void DelString(string sMsg);

        List<csEntry> lData = new List<csEntry>(500);
        LVsorter lvSortObj = new LVsorter();
        bool bSavList = true, bSavText = true, bResize = false;
        short indx = -1, iPage = -1;

        /* Holds manga metadata */
        [Serializable]
        public class csEntry : ISerializable
        {
            public string sArtist, sTitle, 
                sLoc, sType, sDesc, sTags;
            public DateTime dtDate;
            public ushort iPages;
            public byte byRat;

            public csEntry(string _Artist, string _Title, string _Loc, string _Desc,
                string _Tags, string _Type, DateTime _Date, decimal _Pages, int _Rating)
            {
                sArtist = _Artist;
                sTitle = _Title;
                sLoc = _Loc;
                sDesc = _Desc;
                dtDate = _Date;
                iPages = (ushort)_Pages;
                sType = _Type;
                byRat = Convert.ToByte(_Rating);
                
                //trim, clean, and format tags
                if (_Tags == "") {
                    sTags = "";
                    return;
                }
                string[] sRaw = _Tags.Split(',').Select(
                    x => x.Trim()).Distinct().Where(
                    x => !string.IsNullOrEmpty(x)).ToArray<string>();
                sTags = String.Join(", ", sRaw.OrderBy(x => x));
            }
            public csEntry(string _Path)
            {
                //Try to format raw title string
                string[] asTitle = SplitTitle(Path.GetFileNameWithoutExtension(_Path));
                if (asTitle.Length >= 2) {
                    sArtist = asTitle[0];
                    sTitle = asTitle[1];
                } else {
                    sTitle = asTitle[0];
                    sArtist = "";
                }

                sLoc = _Path;
                sType = "Manga";
                sDesc = "";
                sTags = "";
                dtDate = DateTime.Now;
                byRat = 0;

                //Get filecount
                string[] sFiles = new string[0];
                if (File.Exists(_Path))
                    sFiles = new string[1] { _Path };
                else sFiles = ExtDir.GetFiles(_Path,
                    SearchOption.TopDirectoryOnly, "*.zip|*.cbz");

                if (sFiles.Length > 0) {
                    if(Nagru___Manga_Organizer.Main.IsArchive(sFiles[0])) {
                        SCA.IArchive scArchive = SCA.ArchiveFactory.Open(sFiles[0]);
                        iPages = (ushort)scArchive.Entries.Count();
                        scArchive.Dispose();
                    }
                }
                else iPages = (ushort)ExtDir.GetFiles(
                    _Path, SearchOption.TopDirectoryOnly).Length;
            }

            //Returns formatted title of Entry
            public override string ToString()
            {
                return (sArtist != "") ?
                    string.Format("[{0}] {1}", sArtist, sTitle)
                    : sTitle;
            }

            /* Override equals to compare entry titles */
            public bool Equals(csEntry en)
            {
                return (en.sArtist + en.sTitle).Equals(
                    sArtist + sTitle, StringComparison.OrdinalIgnoreCase);
            }

            /* 'Disable' hashtable */
            public override int GetHashCode() { return 1; }

            /* custom serialization to save datatypes manually */
            protected csEntry(SerializationInfo info, StreamingContext ctxt)
            {
                sTitle = info.GetString("TI");
                sArtist = info.GetString("AR");
                sLoc = info.GetString("LO");
                sDesc = info.GetString("DS");
                dtDate = info.GetDateTime("DT");
                sType = info.GetString("TY");
                byRat = info.GetByte("RT");
                sTags = info.GetString("TG");
                iPages = (ushort)info.GetInt32("PG");
            }

            /* custom serialization to read datatypes manually */
            [System.Security.Permissions.SecurityPermission(System.Security.Permissions.
                SecurityAction.LinkDemand, Flags = System.Security.Permissions.
                SecurityPermissionFlag.SerializationFormatter)]
            public virtual void GetObjectData(SerializationInfo info, StreamingContext ctxt)
            {
                info.AddValue("TI", sTitle);
                info.AddValue("AR", sArtist);
                info.AddValue("LO", sLoc);
                info.AddValue("DS", sDesc);
                info.AddValue("DT", dtDate);
                info.AddValue("PG", iPages);
                info.AddValue("TY", sType);
                info.AddValue("RT", byRat);
                info.AddValue("TG", sTags);
            }
        }

        /* Search term processing */
        private class csTerm
        {
            readonly bool[] bAllow;
            readonly string[] sTerm;
            readonly string sType;

            public csTerm(string _Raw)
            {
                //check for type limiter
                sTerm = new string[1];
                string[] sSplit = _Raw.Trim().Split(':');
                if (sSplit.Length == 2) {
                    sType = sSplit[0];
                    sTerm = sSplit[1].Split('&');
                }
                else sTerm = _Raw.Split('&');

                //check for chained terms
                bAllow = new bool[sTerm.Length];
                for(int i = 0; i < sTerm.Length; i++) {
                    sTerm[i] = sTerm[i].Replace('_', ' ');
                    if (sTerm[i].StartsWith("-")) {
                        sTerm[i] = sTerm[i].Substring(1);
                        bAllow[i] = false;
                    }
                    else bAllow[i] = true;
                }
            }

            public bool Equals(csEntry en)
            {
                for(int i = 0; i < sTerm.Length; i++) {
                    bool bMatch = false;
                    switch (sType) {
                        case "artist":
                            bMatch = ExtString.Contains(en.sArtist, sTerm[i]);
                            break;
                        case "title":
                            bMatch = ExtString.Contains(en.sTitle, sTerm[i]);
                            break;
                        case "tag":
                        case "tags":
                            bMatch = ExtString.Contains(en.sTags, sTerm[i]);
                            break;
                        case "desc":
                            bMatch = ExtString.Contains(en.sDesc, sTerm[i]);
                            break;
                        case "type":
                            bMatch = ExtString.Contains(en.sType, sTerm[i]);
                            break;
                        case "date":
                            DateTime dt = new DateTime();
                            char c = sTerm[i] != "" ? sTerm[i][0] : ' ';

                            switch (c) {
                                case '<':
                                    if (DateTime.TryParse(sTerm[i].Substring(1), out dt))
                                        bMatch = dt >= en.dtDate;
                                    break;
                                case '>':
                                    if (DateTime.TryParse(sTerm[i].Substring(1), out dt))
                                        bMatch = dt <= en.dtDate;
                                    break;
                                default:
                                    if (DateTime.TryParse(sTerm[i], out dt))
                                        bMatch = dt == en.dtDate;
                                    break;
                            }
                            break;
                        case "pages":
                            c = sTerm[i] != "" ? sTerm[i][0] : ' ';
                            int x;

                            switch (c) {
                                case '<':
                                    if (int.TryParse(sTerm[i].Substring(1), out x))
                                        bMatch = en.iPages <= x;
                                    break;
                                case '>':
                                    if (int.TryParse(sTerm[i].Substring(1), out x))
                                        bMatch = en.iPages >= x;
                                    break;
                                default:
                                    bMatch = en.iPages.ToString().Equals(sTerm[i]);
                                    break;
                            }
                            break;
                        default:
                            bMatch = (ExtString.Contains(en.sTags, sTerm[i]) ||
                                ExtString.Contains(en.sTitle, sTerm[i]) ||
                                ExtString.Contains(en.sArtist, sTerm[i]) ||
                                ExtString.Contains(en.sDesc, sTerm[i]) ||
                                ExtString.Contains(en.dtDate.ToString(), sTerm[i]) ||
                                ExtString.Contains(en.sType, sTerm[i]));
                            break;
                    }

                    if (!(bAllow[i] ? bMatch : !bMatch))
                        return false;
                }
                return true;
            }
        }

        #region FormMethods
        public Main(string[] sFile)
        {
            InitializeComponent();
            const int iFileName = 18;
            this.Icon = Properties.Resources.dbIcon;

            //if database opened with "Shell->Open with..."
            if (sFile.Length > 0 
                    && sFile[0].EndsWith("\\MangaDatabase.bin") 
                    && Properties.Settings.Default.SavLoc 
                    != sFile[0].Substring(0, sFile[0].Length - iFileName)) {
                Properties.Settings.Default.SavLoc = sFile[0];
                MessageBox.Show("Default database location changed to:\n\"" + sFile[0] + '\"',
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
            //manually handle AssemblyResolve event
            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        /* Load custom library 
           Author: Calle Mellergardh (March 1, 2010) */
        System.Reflection.Assembly CurrentDomain_AssemblyResolve(
            object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("SharpCompress"))
                return (sender as AppDomain).Load(Nagru___Manga_Organizer.
                    Properties.Resources.SharpCompress);
            return null;
        }

        private void Main_Load(object sender, EventArgs e)
        {
            #if !DEBUG
            //Run tutorial on first execution
            if (Properties.Settings.Default.FirstRun) {
                Properties.Settings.Default.FirstRun = false;
                Properties.Settings.Default.Save();

                Tutorial fmTut = new Tutorial();
                fmTut.ShowDialog();
                fmTut.Dispose();

                //set runtime sensitive default locations
                Properties.Settings.Default.SavLoc = Environment.CurrentDirectory;
                Properties.Settings.Default.DefLoc = Environment.CurrentDirectory;
            }
            #endif

            //disable ContextMenu in Nud_Pages
            Nud_Pages.ContextMenuStrip = new ContextMenuStrip();
            
            LV_Entries.ListViewItemSorter = lvSortObj;
            LV_Entries_Resize(sender, e);
            LV_Entries.Select();
            
            frTxBx_Desc.AllowDrop = true;
            frTxBx_Notes.AllowDrop = true;
            frTxBx_Notes.DragDrop += new DragEventHandler(DragDropTxBx);
            frTxBx_Desc.DragDrop += new DragEventHandler(DragDropTxBx);
            frTxBx_Desc.DragEnter += new DragEventHandler(DragEnterTxBx);
            frTxBx_Notes.DragEnter += new DragEventHandler(DragEnterTxBx);
            frTxBx_Notes.Text = Properties.Settings.Default.Notes;

            //check user settings
            this.Location = Properties.Settings.Default.Position.Location;
            this.Width = Properties.Settings.Default.Position.Width;
            this.Height = Properties.Settings.Default.Position.Height;
            LV_Entries.GridLines = Properties.Settings.Default.DefGrid;
            PicBx_Cover.BackColor = Properties.Settings.Default.DefColour;

            //load database
            string sPath = Properties.Settings.Default.SavLoc != string.Empty ?
                Properties.Settings.Default.SavLoc : Environment.CurrentDirectory;
            sPath += "\\MangaDatabase.bin";

            if (lData.Count == 0 && File.Exists(sPath)) {
                lData = FileSerializer.Deserialize<List<csEntry>>(sPath) 
                    ?? new List<csEntry>(0);

                if (lData.Count == 0) {
                    lData = FileSerializer.ConvertDB(sPath);
                    if (lData == null) {
                        MessageBox.Show("The database was invalid.", Application.ProductName,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    bSavList = false;
                }
                UpdateLV();

                //set up artist & tag autocomplete
                string[] asArtists = new string[lData.Count];
                List<string> lTags = new List<string>(lData.Count);
                for (int i = 0; i < lData.Count; i++) {
                    asArtists[i] = lData[i].sArtist;
                    lTags.AddRange(lData[i].sTags.Split(','));
                }
                CmbBx_Artist.Items.AddRange(asArtists.Distinct().Select(x => x).ToArray());
                acTxBx_Tags.KeyWords = lTags.Distinct().Select(x => x.Trim()).ToArray();
            }
        }

        /* Prevent Form close if unsaved data present   */
        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            //save changes to text automatically
            if (!bSavText) {
                Properties.Settings.Default.Notes = frTxBx_Notes.Text;
                bSavText = true;
            }

            //save changes to manga on request
            if (!bSavList) {
                switch (MessageBox.Show("Save before exiting?", Application.ProductName,
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        this.Visible = false;
                        SaveData();
                        break;
                    case DialogResult.Cancel:
                        e.Cancel = true;
                        bSavText = true;
                        break;
                }
            }

            //save Form's last position
            Properties.Settings.Default.Position =
                new Rectangle(this.Location, this.Size);
            Properties.Settings.Default.Save();
        }

        /* Display image from selected folder   */
        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SuspendLayout();
            switch (TabControl.SelectedIndex) {
                case 0:
                    if (indx == -1) {
                        Text = string.Format("{0}: {1:n0} entries",
                            (TxBx_Search.Text == "" && !ChkBx_ShowFav.Checked ?
                            "Manga Organizer" : "Returned"), LV_Entries.Items.Count);
                    }
                    this.AcceptButton = Btn_Clear;
                    LV_Entries.Focus();
                    break;
                case 1:
                    if (indx != -1) {
                        Text = "Selected: " + lData[indx].ToString();
                        MnTS_Del.Visible = true;
                    }
                    this.AcceptButton = null;
                    break;
                case 2:
                    frTxBx_Notes.Select();
                    this.AcceptButton = null;
                    break;
            }
            this.ResumeLayout();
        }

        /* Switch tabs with ctrl+n shortcuts */
        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control) {
                switch (e.KeyCode) {
                    case Keys.D1:
                        TabControl.SelectedIndex = 0;
                        break;
                    case Keys.D2:
                        TabControl.SelectedIndex = 1;
                        break;
                    case Keys.D3:
                        TabControl.SelectedIndex = 2;
                        break;
                }
            }
        }
        #endregion

        #region Tab_Browse
        private void ClearSelection(object sender, EventArgs e)
        { if (indx != -1) Reset(); }

        private void Btn_Scan_Click(object sender, EventArgs e)
        {
            Scan fmScan = new Scan();
            fmScan.delNewEntry = AddEntries;
            fmScan.lCurr = lData;
            fmScan.delDone = fmScanDone;
            Btn_Scan.Enabled = false;

            fmScan.Show();
            fmScan.Select();
        }
        private void fmScanDone() { Btn_Scan.Enabled = true; }
        
        /* Inserts delay before Search() to account for Human input speed */
        private void TxBx_Search_TextChanged(object sender, EventArgs e)
        {
            Reset();
            Delay.Stop();

            if (TxBx_Search.Text == "") {
                TxBx_Search.Width += 30;
                Btn_Clear.Visible = false;
                UpdateLV();
            }
            else if (!Btn_Clear.Visible) {
                TxBx_Search.Width -= 30;
                Btn_Clear.Visible = true;
            }
            else Delay.Start();
        }
        private void Delay_Tick(object sender, EventArgs e)
        {
            Delay.Stop();
            Search();
        }

        private void Btn_Clear_Click(object sender, EventArgs e)
        {
            TxBx_Search.Focus();
            TxBx_Search.Clear();
            UpdateLV();
        }

        private void LV_Entries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (LV_Entries.SelectedItems.Count > 0)
                SetData(Convert.ToInt32(LV_Entries.FocusedItem.SubItems[5].Text));
            else Reset();
        }

        private void LV_Entries_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            //prevent sorting by tags
            if (e.Column == 3) return;

            if (e.Column != lvSortObj.ColToSort)
                lvSortObj.NewColumn(e.Column, SortOrder.Ascending);
            else lvSortObj.SwapOrder();
            
            LV_Entries.Sort();
            Alternate();
        }

        /* Proportionally-resizes columns   */
        private void LV_Entries_Resize(object sender, EventArgs e)
        {
            const int iStatic = 200, iScroll = 20;
            int iMod = LV_Entries.Width / 20;
            LV_Entries.BeginUpdate();
            LV_Entries.Columns[0].Width = iMod * 3;
            LV_Entries.Columns[1].Width = iMod * 6;
            LV_Entries.Columns[3].Width = iMod * 6;

            /* set ColTags to remaining listview width  */
            ColTags.Width += LV_Entries.Width - (iMod * 15) - iStatic;
            if(LV_Entries.Items.Count > LV_Entries.Height / 20)
                ColTags.Width -= iScroll;
            LV_Entries.EndUpdate();
        }

        /* Prevent user from changing column widths */
        private void LV_Entries_ColumnWidthChanging(
            object sender, ColumnWidthChangingEventArgs e)
        {
            e.Cancel = true;
            e.NewWidth = LV_Entries.Columns[e.ColumnIndex].Width;
        }

        private void LV_Entries_DoubleClick(object sender, EventArgs e)
        { OpenFile(); }

        /* More convenient listview focusing */
        private void LV_Entries_MouseHover(object sender, EventArgs e)
        {
            if (!LV_Entries.Focused && !Delay.Enabled)
                LV_Entries.Focus();
        }

        private void ChkBx_ShowFav_CheckedChanged(object sender, EventArgs e)
        {
            if (ChkBx_ShowFav.Checked) OnlyFavs();
            else UpdateLV();

            if (indx != -1 && lData[indx].byRat < 5) ReFocus();
            else Reset();

            LV_Entries.Select();
        }
        #endregion

        #region Tab_View
        /* Select location of manga entry */
        private void Btn_Loc_Click(object sender, EventArgs e)
        {
            string sPath = TxBx_Loc.Text;

            //try to auto-magically grab folder\file path
            if (!File.Exists(sPath) && !Directory.Exists(sPath)) {
                sPath = string.Format("{0}\\[{1}] {2}",
                    Properties.Settings.Default.DefLoc,
                    CmbBx_Artist.Text, TxBx_Title.Text);
                if (!Directory.Exists(sPath)) {
                    if (File.Exists(sPath + ".zip"))
                        sPath += ".zip";
                    else if (File.Exists(sPath + ".cbz"))
                        sPath += ".cbz";
                    else if (File.Exists(sPath + ".rar"))
                        sPath += ".rar";
                    else if (File.Exists(sPath + ".cbr"))
                        sPath += ".cbr";
                    else if (File.Exists(sPath + ".7z"))
                        sPath += ".7z";
                    else sPath = Properties.Settings.Default.DefLoc;
                }
            }

            ExtFolderBrowserDialog xfbd = new ExtFolderBrowserDialog();
            xfbd.ShowBothFilesAndFolders = true;
            xfbd.RootFolder = Environment.SpecialFolder.MyComputer;
            xfbd.SelectedPath = sPath;

            if (xfbd.ShowDialog() == DialogResult.OK) {
                TxBx_Loc.Text = xfbd.SelectedPath;
                ThreadPool.QueueUserWorkItem(GetImage);

                if (CmbBx_Artist.Text == "" && TxBx_Title.Text == "")
                    SplitTitleSet(SplitTitle(Path.GetFileName(xfbd.SelectedPath)));
            }
            xfbd.Dispose();
        }

        /* Open URL in default Browser  */
        private void frTxBx_Desc_LinkClicked(object sender, LinkClickedEventArgs e)
        { System.Diagnostics.Process.Start(e.LinkText); }

        /* Alternative to MnTS_Open */
        private void PicBx_Cover_Click(object sender, EventArgs e)
        {
            if (PicBx_Cover.Image == null)
                return;

            Browse_Img fmBrowse = new Browse_Img();
            fmBrowse.Page = iPage;

            if(Directory.Exists(TxBx_Loc.Text)) {
                //process 'loose' images
                string[] sFiles = new string[0];
                if ((sFiles = ExtDir.GetFiles(TxBx_Loc.Text,
                        SearchOption.TopDirectoryOnly)).Length > 0) {
                    fmBrowse.Files = new List<string>(sFiles.Length);
                    fmBrowse.Files.AddRange(sFiles);
                    fmBrowse.ShowDialog();
                    iPage = (short)fmBrowse.Page;
                }
            } else {
                //process compressed images
                SCA.IArchive scArchive = SCA.ArchiveFactory.Open(@TxBx_Loc.Text);
                if (scArchive.Entries.Count() > 0) {
                    SCA.IArchiveEntry[] scEntries = scArchive.Entries.ToArray();
                    fmBrowse.Files = new List<string>(scEntries.Length);
                    for (int i = 0; i < scEntries.Length; i++) {
                        fmBrowse.Files.Add(scEntries[i].FilePath);
                    }
                    fmBrowse.Archive = scEntries;

                    fmBrowse.ShowDialog();
                    iPage = (short)fmBrowse.Page;
                }
                scArchive.Dispose();
            }
            fmBrowse.Dispose();
            GC.Collect(0);
        }

        /* Redraw cover image if size has changed */
        private void PicBx_Cover_Resize(object sender, EventArgs e)
        {
            if (PicBx_Cover.Image == null) return;
            SizeF sf = PicBx_Cover.Image.PhysicalDimension;
            if(sf.Width < sf.Height) {
                if (PicBx_Cover.Height > sf.Height)
                    bResize = true;
            }
            else /*if (sf.Width > sf.Height)*/ {
                if (PicBx_Cover.Width - 1 > sf.Width)
                    bResize = true;
            }
        }
        private void Main_ResizeEnd(object sender, EventArgs e)
        {
            if (bResize) {
                ThreadPool.QueueUserWorkItem(GetImage);
                bResize = false;
            }
        }

        /* Dynamically update PicBx when user manually alters path */
        private void TxBx_Loc_TextChanged(object sender, EventArgs e)
        {
            if (indx != -1) MnTS_Edit.Visible = true;

            if (Directory.Exists(TxBx_Loc.Text)
                    || File.Exists(TxBx_Loc.Text)) {
                iPage = -1;
                ThreadPool.QueueUserWorkItem(GetImage);
            }
            else {
                SetPicBxNull();
                SetOpenStatus(0);
            }
        }

        /* Programmatically select item in LV_Entries  */
        private void Btn_GoDn_Click(object sender, EventArgs e)
        {
            if (LV_Entries.Items.Count == 0) return;
            int iPos = 0;

            if (LV_Entries.SelectedItems.Count == 1) {
                iPos = LV_Entries.SelectedItems[0].Index;
                if (++iPos >= LV_Entries.Items.Count) iPos = 0;
            }

            ScrollTo(iPos);
        }
        private void Btn_GoUp_Click(object sender, EventArgs e)
        {
            if (LV_Entries.Items.Count == 0) return;
            int iPos = LV_Entries.Items.Count - 1;

            if (LV_Entries.SelectedItems.Count == 1) {
                iPos = LV_Entries.SelectedItems[0].Index;
                if (--iPos < 0) iPos = LV_Entries.Items.Count - 1;
            }

            ScrollTo(iPos);
        }
        private void Btn_Rand_Click(object sender, EventArgs e)
        {
            switch (LV_Entries.Items.Count) {
                case 0: return;
                case 1: if(indx != -1) return;
                    break;
                default: break;
            }
            
            Random rnd = new Random();
            int iCur = LV_Entries.SelectedItems.Count == 1 ?
                LV_Entries.SelectedItems[0].Index : -1;
            int iNew = 0;
            
            do {
                iNew = rnd.Next(LV_Entries.Items.Count);
            } while (iNew == iCur);
            ScrollTo(iNew);
        }

        private void srRating_Click(object sender, EventArgs e)
        {
            if (indx == -1 || lData[indx].byRat == srRating.SelectedStar) 
                return;

            //update rating
            lData[indx].byRat = (byte)srRating.SelectedStar;
            LV_Entries.SelectedItems[0].SubItems[6].Text = 
                RatingFormat(lData[indx].byRat);
            bSavList = false;

            //set BackColor
            if (lData[indx].byRat == 5)
                LV_Entries.FocusedItem.BackColor = Color.LightYellow;
            else {
                if (LV_Entries.FocusedItem.Index % 2 == 0)
                    LV_Entries.FocusedItem.BackColor = Color.FromArgb(245, 245, 245);
                else LV_Entries.FocusedItem.BackColor = SystemColors.Window;
            }
        }

        /* Only enable edit when changes have been made */
        private void EntryAlt_Text(object sender, EventArgs e)
        {
            if (indx != -1) 
                MnTS_Edit.Visible = true;
        }
        private void EntryAlt_DtNum(object sender, EventArgs e)
        { if (indx != -1) MnTS_Edit.Visible = true; }
        #endregion

        #region Tab_Notes
        /* Prevent loss of changes in note text   */
        private void frTxBx_Notes_TextChanged(object sender, EventArgs e)
        { if (bSavText) bSavText = false; }

        /* Open URL in default Browser  */
        private void frTxBx_Notes_LinkClicked(object sender, LinkClickedEventArgs e)
        { System.Diagnostics.Process.Start(e.LinkText); }
        #endregion

        #region CustomMethods
        private void AddEntries()
        {
            UpdateLV();
            bSavList = false;
            LV_Entries.Select();
            if (indx != -1) ReFocus();
        }

        /* Alternate row colors in listview */
        private void Alternate(int iStart = 0)
        {
            if (Properties.Settings.Default.DefGrid) return;
            for (int i = iStart; i < LV_Entries.Items.Count; i++) {
                if (LV_Entries.Items[i].BackColor == Color.LightYellow)
                    continue;
                if (i % 2 != 0)
                    LV_Entries.Items[i].BackColor = Color.FromArgb(245, 245, 245);
                else LV_Entries.Items[i].BackColor = SystemColors.Window;
            }
        }

        private void GetImage(Object obj)
        {
            BeginInvoke(new DelVoid(SetPicBxNull));

            //Get cover and filecount
            if(File.Exists(TxBx_Loc.Text)) {
                SetPicBxImage(TxBx_Loc.Text);
            } else {
                string[] sFiles = new string[0];
                if ((sFiles = ExtDir.GetFiles(TxBx_Loc.Text,
                    SearchOption.TopDirectoryOnly)).Length > 0) {
                    SetPicBxImage(sFiles[0]);
                    BeginInvoke(new DelInt(SetNudCount), sFiles.Length);
                } else {
                    BeginInvoke(new DelInt(SetOpenStatus), 0);
                }
            }
        }

        private static int InsertText(Control c, string sAdd, int iStart)
        {
            int iNewStart = iStart + sAdd.Length;
            c.Text = c.Text.Insert(iStart, sAdd);
            return iNewStart;
        }

        private static bool IsArchive(string sPath)
        {
            if (File.Exists(sPath)
                    && (SCA.Rar.RarArchive.IsRarFile(sPath)
                    || SCA.Zip.ZipArchive.IsZipFile(sPath)
                    || SCA.SevenZip.SevenZipArchive.IsSevenZipFile(sPath))) {
                return true;
            } else {
                return false;
            }
        }

        /* Used to simulate JS Object Literal for JSON 
           Based on Hupotronics' ExLinks   */
        private static string JSON(string sURL)
        {
            string[] asChunk = sURL.Split('/');
            if (asChunk.Length == 7) {
                return string.Format(
                    "{{\"method\":\"gdata\",\"gidlist\":[[{0},\"{1}\"]]}}",
                    asChunk[4], asChunk[5]);
            }
            return string.Empty;
        }

        /* Remove all entries less than 5 stars */
        private void OnlyFavs()
        {
            Cursor = Cursors.WaitCursor;
            LV_Entries.BeginUpdate();
            for (int i = 0; i < LV_Entries.Items.Count; i++) {
                if(LV_Entries.Items[i].BackColor != Color.LightYellow)
                    LV_Entries.Items.RemoveAt(i--);
            }
            LV_Entries.EndUpdate();
            Text = string.Format("Returned: {0:n0} entries", LV_Entries.Items.Count);
            Cursor = Cursors.Default;
        }

        /* Open image\zip with default program */
        private void OpenFile()
        {
            if (PicBx_Cover.Image == null)
                return;

            if(Directory.Exists(TxBx_Loc.Text)) {
                string[] sFiles = ExtDir.GetFiles(@TxBx_Loc.Text);
                if (sFiles.Length > 0) System.Diagnostics.Process.Start(sFiles[0]);
            }
            else System.Diagnostics.Process.Start(@TxBx_Loc.Text);
        }

        /* Convert number to string of stars */
        private static string RatingFormat(byte byVal)
        {
            System.Text.StringBuilder sb =
                new System.Text.StringBuilder("");
            sb.Append('☆', byVal);
            return sb.ToString();
        }

        private void ReFocus()
        {
            if (LV_Entries.Items.Count == 0)
                return;

            for (int i = 0; i < LV_Entries.Items.Count; i++)
                if (LV_Entries.Items[i].SubItems[1].Text == lData[indx].sTitle) {
                    ScrollTo(i);
                    break;
                }
        }

        private void Reset()
        {
            //reset Form title
            Tb_View.SuspendLayout();
            Text = string.Format("{0}: {1:n0} entries",
                (TxBx_Search.Text == "" && !ChkBx_ShowFav.Checked ? 
                "Manga Organizer" : "Returned"), LV_Entries.Items.Count);

            //Tb_Browse
            LV_Entries.FocusedItem = null;
            LV_Entries.SelectedItems.Clear();
            iPage = -1;
            indx = -1;

            //Tb_View
            TxBx_Loc.Clear();
            acTxBx_Tags.Clear();
            TxBx_Title.Clear();
            frTxBx_Desc.Clear();
            Nud_Pages.Value = 0;
            CmbBx_Artist.Text = "";
            CmbBx_Type.Text = "Manga";
            Dt_Date.Value = DateTime.Now;
            srRating.SelectedStar = 0;
            SetPicBxNull();

            //Mn_EntryOps
            acTxBx_Tags.SetScroll();
            MnTS_New.Visible = true;
            MnTS_Del.Visible = false;
            MnTS_Edit.Visible = false;
            MnTS_Open.Visible = false;
            Tb_View.ResumeLayout();
        }

        private void SaveData()
        {
            Cursor = Cursors.WaitCursor;
            Text = "Saving...";

            //grab filepath
            string sPath = Properties.Settings.Default.SavLoc;
            if (sPath != string.Empty && Directory.Exists(sPath))
                sPath += "\\MangaDatabase.bin";
            else sPath = Environment.CurrentDirectory + "\\MangaDatabase.bin";

            if (!bSavList) {
                lData.Sort((eX, eY) => (eX.sArtist + eX.sTitle)
                    .CompareTo(eY.sArtist + eY.sTitle));
                if (File.Exists(sPath)) File.Delete(sPath);
                FileSerializer.Serialize(sPath, lData);
                bSavList = true;
            }
            if(!bSavText) {
                Properties.Settings.Default.Save();
                bSavText = true;
            }

            Text = "Saved";
            Cursor = Cursors.Default;
        }

        private void ScrollTo(int iPos)
        {
            LV_Entries.FocusedItem = LV_Entries.Items[iPos];
            LV_Entries.Items[iPos].Selected = true;

            /* Compensate for broken scroll-to function
             * by running it multiple times (3 is sweet-spot) */
            LV_Entries.TopItem = LV_Entries.Items[iPos];
            LV_Entries.TopItem = LV_Entries.Items[iPos];
            LV_Entries.TopItem = LV_Entries.Items[iPos];
        }

        /* Filter lvi by search terms (Check MnTs_Edit after changes)  */
        private void Search()
        {
            Cursor = Cursors.WaitCursor;
            string[] sTags = TxBx_Search.Text.Split(' ');
            csTerm[] aTerms = new csTerm[sTags.Length];
            List<ListViewItem> lItems = new List<ListViewItem>(lData.Count);

            //format text into search parameters
            for (int i = 0; i < sTags.Length; i++)
                aTerms[i] = new csTerm(sTags[i]);
            
            //compare entries to search parameters
            for (int i = 0; i < lData.Count; i++) {
                bool bInc = true;
                for (int n = 0; n < aTerms.Length; n++) {
                    if (!aTerms[n].Equals(lData[i])) {
                        bInc = false;
                        break;
                    }
                }
                //reject entry if no match (or filtered by param)
                if (!bInc) continue;

                //else add to list
                ListViewItem lvi = new ListViewItem(lData[i].sArtist);
                if (lData[i].byRat == 5) lvi.BackColor = Color.LightYellow;
                lvi.SubItems.AddRange(new string[] {
                        lData[i].sTitle,
                        lData[i].iPages.ToString(),
                        lData[i].sTags,
                        lData[i].sType,
                        i.ToString(),
                        RatingFormat(lData[i].byRat)
                    });
                lItems.Add(lvi);
            }
            
            LV_Entries.BeginUpdate();
            LV_Entries.Items.Clear();
            LV_Entries.Items.AddRange(lItems.ToArray());
            LV_Entries.Sort();
            Alternate();
            LV_Entries.EndUpdate();
            Text = string.Format("Returned: {0:n0} entries", LV_Entries.Items.Count);
            Cursor = Cursors.Default;

            //prevent loss of other parameters
            if (ChkBx_ShowFav.Checked) OnlyFavs();
        }

        private void SetData(int iNewIndx)
        {
            if ((indx = (short)iNewIndx) == -1)
            { Reset(); return; }
            
            Tb_View.SuspendLayout();
            Text = "Selected: " + lData[indx].ToString();
            TxBx_Title.Text = lData[indx].sTitle;
            CmbBx_Artist.Text = lData[indx].sArtist;
            TxBx_Loc.Text = lData[indx].sLoc;
            frTxBx_Desc.Text = lData[indx].sDesc;
            CmbBx_Type.Text = lData[indx].sType;
            Dt_Date.Value = lData[indx].dtDate;
            srRating.SelectedStar = lData[indx].byRat;
            Nud_Pages.Value = lData[indx].iPages;
            acTxBx_Tags.Text = lData[indx].sTags;

            acTxBx_Tags.SetScroll();
            MnTS_New.Visible = false;
            MnTS_Edit.Visible = false;
            MnTS_Del.Visible = true;
            Tb_View.ResumeLayout();
        }

        private void SetNudCount(int iNum)
        {
            Nud_Pages.Value = iNum;
            TxBx_Loc.SelectionStart = TxBx_Loc.Text.Length;
            SetOpenStatus(1);
        }

        private void SetOpenStatus(int iExists)
        {
            MnTS_Open.Visible = (iExists == 1);
        }

        private void SetPicBxImage(string sPath)
        {
            if(IsArchive(sPath)) {
                SCA.IArchive scArch = SCA.ArchiveFactory.Open(sPath);
                int iCount = scArch.Entries.Count();
                BeginInvoke(new DelInt(SetNudCount), iCount);

                if(iCount > 0) {
                    //account for terrible default zip-sorting
                    int iFirst = 0;
                    SCA.IArchiveEntry[] scEntries = 
                        scArch.Entries.ToArray();
                    List<string> lEntries = new List<string>();
                    for(int i = 0; i < iCount; i++) {
                        if(scEntries[i].FilePath.EndsWith("jpg")
                                || scEntries[i].FilePath.EndsWith("jpeg")
                                || scEntries[i].FilePath.EndsWith("png")
                                || scEntries[i].FilePath.EndsWith("bmp"))
                            lEntries.Add(scEntries[i].FilePath);
                    }
                    if (lEntries.Count == 0) return;
                    lEntries.Sort(new TrueCompare());
                    for (int i = 0; i < iCount; i++) {
                        if(scEntries[i].FilePath.Equals(lEntries[0]))
                            break;
                        iFirst++;
                    }

                    //load image
                    MemoryStream ms = new MemoryStream();
                    try {
                        scEntries[iFirst].WriteTo(ms);
                        using (Bitmap bmpTmp = new Bitmap(ms)) {
                            PicBx_Cover.Image = ExtImage.Scale(bmpTmp,
                                PicBx_Cover.Width, PicBx_Cover.Height);
                        }
                    } catch (Exception Exc) {
                        MessageBox.Show("The following file could not be loaded:\n" + 
                            sPath + '\\' + scEntries[iFirst].FilePath,
                            Application.ProductName, MessageBoxButtons.OK, 
                            MessageBoxIcon.Exclamation);
                        Console.WriteLine(Exc.Message);
                    } finally {
                        ms.Dispose();
                    }
                }
                scArch.Dispose();
            } else {
                TrySet(sPath);
            }
        }
        private void TrySet(string s)
        {
            try {
                using (Bitmap bmpTmp = new Bitmap(s)) {
                    PicBx_Cover.Image = ExtImage.Scale(bmpTmp,
                        PicBx_Cover.Width, PicBx_Cover.Height);
                }
            } catch (Exception Exc) {
                MessageBox.Show("The following file could not be loaded:\n" + s,
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Console.WriteLine(Exc.Message);
            } finally {
                GC.Collect(0);
            }
        }

        private void SetPicBxNull()
        {
            if (PicBx_Cover.Image != null) {
                PicBx_Cover.Image.Dispose();
                PicBx_Cover.Image = null;
                GC.Collect(0);
            }
        }

        private static string[] SplitTitle(string sRaw)
        {
            //strip out comiket\circle\etc info
            if (sRaw.StartsWith("(")) {
                int iPos = sRaw.IndexOf(')') + 2;
                if (sRaw.Length - 1 >= iPos)
                    sRaw = sRaw.Remove(0, iPos);
            }
            string[] sName = ExtString.Split(sRaw, "[", "]");
            
            //Send to artist/title fields
            if (sName.Length >= 2) {
                //strip out scanlation info
                if (sName[0].Contains('(')) {
                    sName[0] = sName[0].Substring(sName[0].IndexOf('(') + 1);
                    sName[0] = sName[0].Substring(0, sName[0].Length - 1);
                }
                sName[0] = sName[0].Trim();

                //strip out parody info
                if (sName[1].Contains('(')) {
                    sName[1] = sName[1].Substring(0, sName[1].IndexOf('('));
                }
                sName[1] = sName[1].Trim();
            }
            return sName;
        }
        private void SplitTitleSet(string[] asProc)
        {
            if(asProc.Length >= 2) {
                if (CmbBx_Artist.Text == "")
                    CmbBx_Artist.Text = asProc[0];
                if (TxBx_Title.Text == "")
                    TxBx_Title.Text = asProc[1];
            }
            else {
                TxBx_Title.Text = TxBx_Title.Text.Insert(
                    TxBx_Title.SelectionStart, asProc[0]);
                TxBx_Title.SelectionStart += asProc[0].Length;
            }
        }

        /* Refresh listview contents */
        private void UpdateLV()
        {
            if (TxBx_Search.Text != "") { 
                Search(); return;
            }

            Cursor = Cursors.WaitCursor;
            Text = string.Format("Manga Organizer: {0:n0} entries", lData.Count);
            ListViewItem[] aItems = new ListViewItem[lData.Count];

            for (int i = 0; i < lData.Count; i++) {
                ListViewItem lvi = new ListViewItem(lData[i].sArtist);
                if (lData[i].byRat == 5) lvi.BackColor = Color.LightYellow;
                lvi.SubItems.AddRange(new string[] {
                    lData[i].sTitle,
                    lData[i].iPages.ToString(),
                    lData[i].sTags,
                    lData[i].sType,
                    i.ToString(),
                    RatingFormat(lData[i].byRat)
                });
                aItems[i] = lvi;
            }

            LV_Entries.BeginUpdate();
            LV_Entries.Items.Clear();
            LV_Entries.Items.AddRange(aItems);
            LV_Entries.Sort();
            Alternate();
            LV_Entries.EndUpdate();
            Cursor = Cursors.Default;

            //prevent loss of other parameters
            if (ChkBx_ShowFav.Checked) OnlyFavs();
        }
        #endregion

        #region EntryOps
        private void MnTS_New_Click(object sender, EventArgs e)
        {
            //reject when title is unfilled
            if (TxBx_Title.Text == "") {
                MessageBox.Show("Title cannot be empty.", Application.ProductName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            csEntry en = new csEntry(CmbBx_Artist.Text, TxBx_Title.Text, 
                TxBx_Loc.Text, frTxBx_Desc.Text, acTxBx_Tags.Text, CmbBx_Type.Text,
                Dt_Date.Value.Date, Nud_Pages.Value, srRating.SelectedStar);

            if (!lData.Contains(en)) {
                if (MessageBox.Show("Are you sure you wish to add:\n\"" + en + "\"",
                        Application.ProductName, MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Question) == DialogResult.Yes) {
                    lData.Add(en);

                    //add artist to autocomplete
                    if (!CmbBx_Artist.Items.Contains(CmbBx_Artist.Text)) {
                        CmbBx_Artist.AutoCompleteCustomSource.Add(CmbBx_Artist.Text);
                        CmbBx_Artist.Items.Add(CmbBx_Artist.Text);
                    }
                    acTxBx_Tags.UpdateAutoComplete();

                    //update LV_Entries & maintain scroll position
                    int iPos = LV_Entries.TopItem == null ? -1 : LV_Entries.TopItem.Index;
                    AddEntries();
                    Reset();
                }
            }
            else MessageBox.Show("This item already exists in the database.",
                Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        private void MnTS_Edit_Click(object sender, EventArgs e)
        {
            if (indx == -1) return;

            //overwrite entry properties
            lData[indx] = new csEntry(CmbBx_Artist.Text, TxBx_Title.Text, 
                TxBx_Loc.Text, frTxBx_Desc.Text, acTxBx_Tags.Text, CmbBx_Type.Text,
                Dt_Date.Value, Nud_Pages.Value, srRating.SelectedStar);
            Text = "Edited entry: " + lData[indx].ToString();
            acTxBx_Tags.Text = lData[indx].sTags;
            acTxBx_Tags.UpdateAutoComplete();

            //update LV_Entries & maintain selection
            ListViewItem lvi = LV_Entries.FocusedItem;
            if (lData[indx].byRat == 5) 
                lvi.BackColor = Color.LightYellow;
            lvi.SubItems[0].Text = lData[indx].sArtist;
            lvi.SubItems[1].Text = lData[indx].sTitle;
            lvi.SubItems[2].Text = lData[indx].iPages.ToString();
            lvi.SubItems[3].Text = lData[indx].sTags;
            lvi.SubItems[4].Text = lData[indx].sType;
            LV_Entries.Sort();

            //check if entry should still be displayed
            int iPos = lvi.Index;
            if (ChkBx_ShowFav.Checked 
                    && !(lData[indx].byRat == 5))  {
                lvi.Remove();
            }
            else if (TxBx_Search.Text != "") {
                string[] sTags = TxBx_Search.Text.Split(' ');
                List<csTerm> lTerms = new List<csTerm>(5);

                for (int i = 0; i < sTags.Length; i++)
                    lTerms.Add(new csTerm(sTags[i]));

                for (int y = 0; y < lTerms.Count; y++) {
                    if (!lTerms[y].Equals(lData[indx])) {
                        lvi.Remove();
                        break;
                    }
                }
                Text = string.Format("Returned: {0:n0} entries", LV_Entries.Items.Count);
            }
            else ReFocus();

            Alternate(iPos);
            bSavList = false;
            MnTS_Edit.Visible = false;
        }

        private void MnTS_Open_Click(object sender, EventArgs e)
        {
            if (PicBx_Cover.Image != null)
                OpenFile();
        }

        private void MnTS_Delete_Click(object sender, EventArgs e)
        {
            if (indx == -1) return;

            //ensure deletion is intentional
            string msg = ""; bool bFile = false, bRst = true;
            if (!(bRst = ExtDir.Restricted(lData[indx].sLoc)))
                msg = "Do you want to delete the source directory as well?";
            else if (bFile = File.Exists(lData[indx].sLoc))
                msg = "Do you want to delete the source file as well?";
            else msg = "Are you sure you wish to delete this entry?";
            DialogResult dResult = MessageBox.Show(msg, Application.ProductName, 
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if ((bRst && !bFile) && dResult == DialogResult.No)
                dResult = DialogResult.Cancel;

            //delete source file\directory
            if (dResult == DialogResult.Yes) {
                if (!bRst) {
                    //warn user before deleting subdirectories
                    int iNumDir = Directory.GetDirectories(lData[indx].sLoc).Length;
                    if (iNumDir > 0) {
                        dResult = MessageBox.Show(string.Format("This directory contains {0} subfolder(s),\n" +
                            "are you sure you want to delete them?", iNumDir),
                            Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    }
                    if (dResult == DialogResult.Yes) {
                        this.Cursor = Cursors.WaitCursor;
                        Directory.Delete(lData[indx].sLoc, true);
                        this.Cursor = Cursors.Default;
                    }
                }
                else if (bFile) {
                    this.Cursor = Cursors.WaitCursor;
                    File.Delete(lData[indx].sLoc);
                    this.Cursor = Cursors.Default;
                }
            }
            
            //remove from database
            if (dResult != DialogResult.Cancel) {
                bSavList = false;
                int iPos = LV_Entries.FocusedItem.Index;
                lData.RemoveAt(indx);
                UpdateLV();
                Reset();

                if(iPos < LV_Entries.Items.Count) {
                    LV_Entries.TopItem = LV_Entries.Items[iPos];
                    LV_Entries.TopItem = LV_Entries.Items[iPos];
                    LV_Entries.TopItem = LV_Entries.Items[iPos];
                }
            }
        }

        private void MnTS_Clear_Click(object sender, EventArgs e)
        { Reset(); }
        #endregion

        #region Menu_Main
        private void MnTS_CopyTitle_Click(object sender, EventArgs e)
        {
            if(TxBx_Title.Text == "") {
                MessageBox.Show("The title field cannot be empty.",
                    Application.ProductName, MessageBoxButtons.OK, 
                    MessageBoxIcon.Exclamation);
                return;
            }

            Clipboard.SetText(string.Format((CmbBx_Artist.Text != "") 
                ? "[{0}] {1}" : "{1}", CmbBx_Artist.Text, TxBx_Title.Text));
            Text = "Name copied to clipboard";
        }

        private void MnTS_OpenSource_Click(object sender, EventArgs e)
        {
            if (!ExtDir.Restricted(TxBx_Loc.Text))
                System.Diagnostics.Process.Start(TxBx_Loc.Text);
        }

        /* Uses EH API to get metadata from gallery URL */
        private void MnTS_LoadUrl_Click(object sender, EventArgs e)
        {
            GetUrl fmGet = new GetUrl();
            fmGet.StartPosition = FormStartPosition.Manual;
            fmGet.Location = this.Location;
            fmGet.ShowDialog();

            //process url
            if (fmGet.DialogResult == DialogResult.OK)
            {
                bool bExc = false;
                string[] asResp = new string[0];
                this.Cursor = Cursors.WaitCursor;
                Text = "Sending request...";

                try
                {
                    //send formatted request to EH API
                    ServicePointManager.DefaultConnectionLimit = 64;
                    HttpWebRequest rq = (HttpWebRequest)
                        WebRequest.Create("http://g.e-hentai.org/api.php");
                    rq.ContentType = "application/json";
                    rq.Method = "POST";
                    rq.Timeout = 5000;
                    rq.KeepAlive = false;
                    rq.Proxy = null;

                    using (Stream s = rq.GetRequestStream()) {
                        byte[] byContent = System.Text.Encoding.ASCII.GetBytes(JSON(fmGet.Url));
                        s.Write(byContent, 0, byContent.Length);
                    }
                    using (StreamReader sr = new StreamReader((
                        (HttpWebResponse)rq.GetResponse()).GetResponseStream())) {
                        Text = "Downloading page...";
                        asResp = ExtString.Split(sr.ReadToEnd(), "\",\"");
                        rq.Abort();
                    }
                } catch {
                    bExc = true;
                    Text = "The URL was invalid or the connection timed out.";
                } finally {
                    //parse metadata
                    if(!bExc && asResp.Length >= 11) {
                        Text = "Parsing metadata...";
                        
                        //set artist/title
                        string sRaw = asResp[2].Split(':')[1].Substring(1);
                        SplitTitleSet(SplitTitle(ExtString.ReplaceHTML(sRaw)));

                        //set entry type
                        CmbBx_Type.Text = asResp[4].Split(':')[1].Substring(1);

                        //set date
                        DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        sRaw = asResp[7].Split(':')[1].Substring(1);
                        Dt_Date.Value = dt.AddSeconds((long)Convert.ToDouble(sRaw));

                        //set page count
                        Nud_Pages.Value = Convert.ToInt32(asResp[8].Split(':')[1].Substring(1));

                        //set star rating
                        sRaw = asResp[9].Split(':')[3].Substring(1);
                        srRating.SelectedStar = (int)Convert.ToDouble(sRaw);

                        //set and formart tags
                        asResp[11] = asResp[11].Split(':')[1].Substring(2);
                        acTxBx_Tags.Text = string.Join(", ", asResp, 11, asResp.Length - 11);
                        acTxBx_Tags.Text = acTxBx_Tags.Text.Substring(0, acTxBx_Tags.Text.Length - 5);
                        
                        Text = "Finished";
                    } else {
                        MessageBox.Show("The URL was invalid or the connection timed out.",
                            Application.ProductName, MessageBoxButtons.OK, 
                            MessageBoxIcon.Exclamation);
                    }
                    this.Cursor = Cursors.Default;
                }
            }
            fmGet.Dispose();
        }

        private void MnTS_Save_Click(object sender, EventArgs e)
        { SaveData(); }

        private void MnTS_OpenDataFolder_Click(object sender, EventArgs e)
        {
            string sPath = Properties.Settings.Default.SavLoc != "" ?
                Properties.Settings.Default.SavLoc : Environment.CurrentDirectory;

            if (Directory.Exists(sPath))
                System.Diagnostics.Process.Start(sPath);
            else MessageBox.Show("This directory no longer exists.", Application.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void MnTS_Stats_Click(object sender, EventArgs e)
        {
            Stats fmStats = new Stats();
            fmStats.lCurr = lData;
            fmStats.Show();
        }

        private void Mn_Settings_Click(object sender, EventArgs e)
        {
            string sOld = Properties.Settings.Default.SavLoc;
            Form fmSet = new Settings();
            fmSet.ShowDialog();

            if (fmSet.DialogResult == DialogResult.Yes)
            {
                LV_Entries.GridLines = Properties.Settings.Default.DefGrid;
                PicBx_Cover.BackColor = Properties.Settings.Default.DefColour;

                //Update new DB save location
                if (sOld != Properties.Settings.Default.SavLoc)
                {
                    string sNew = Properties.Settings.Default.SavLoc + "\\MangaDatabase.bin";
                    sOld += "\\MangaDatabase.bin";

                    //move old save to new location
                    if (File.Exists(sNew)
                            && MessageBox.Show("Open existing database at:\n" + sNew,
                            Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                            == DialogResult.Yes)
                    {
                        lData = FileSerializer.Deserialize<List<csEntry>>(sNew);

                        if (lData != null)
                        {
                            UpdateLV();

                            //set up CmbBx autocomplete
                            List<string> lAuto = new List<string>(lData.Count);
                            for (int i = 0; i < lData.Count; i++) lAuto.Add(lData[i].sArtist);
                            lAuto.Sort(new TrueCompare());
                            string[] sFinal = lAuto.Distinct().ToArray();
                            CmbBx_Artist.Items.Clear();
                            CmbBx_Artist.Items.AddRange(sFinal);
                        }
                    }
                    else if (File.Exists(sOld))
                        File.Move(sOld, sNew);

                    Text = "Default save location changed";
                }
            }
            fmSet.Dispose();
        }

        private void MnTS_Tutorial_Click(object sender, EventArgs e)
        {
            Tutorial fmTut = new Tutorial();
            fmTut.ShowDialog();
            fmTut.Dispose();
        }

        private void MnTS_About_Click(object sender, EventArgs e)
        {
            About fmAbout = new About();
            fmAbout.ShowDialog();
            fmAbout.Dispose();
        }

        private void MnTs_Quit_Click(object sender, EventArgs e)
        { this.Close(); }
        #endregion

        #region Menu_Context
        private void MnTx_Undo_Click(object sender, EventArgs e)
        {
            switch (ActiveControl.GetType().Name) {
                case "FixedRichTextBox":
                    ((FixedRichTextBox)ActiveControl).Undo();
                    if (TabControl.SelectedIndex == 2 && !frTxBx_Notes.CanUndo) 
                        bSavText = true;
                    break;
                case "TextBox":
                case "AutoCompleteTagger":
                    ((TextBox)ActiveControl).Undo();
                    break;
                case "ComboBox":
                    ((ComboBox)ActiveControl).ResetText();
                    break;
            }
        }

        private void MnTx_Cut_Click(object sender, EventArgs e)
        {
            switch (ActiveControl.GetType().Name) {
                case "FixedRichTextBox":
                    ((FixedRichTextBox)ActiveControl).Cut();
                    if (TabControl.SelectedIndex == 2) bSavText = false;
                    break;
                case "TextBox":
                case "AutoCompleteTagger":
                    ((TextBox)ActiveControl).Cut();
                    break;
                case "ComboBox":
                    ComboBox cx = (ComboBox)ActiveControl;
                    if (cx.SelectedText != "") {
                        Clipboard.SetText(cx.SelectedText);
                        cx.SelectedText = "";
                    }
                    break;
            }
        }

        private void MnTx_Copy_Click(object sender, EventArgs e)
        {
            switch (ActiveControl.GetType().Name) {
                case "FixedRichTextBox":
                    ((FixedRichTextBox)ActiveControl).Copy();
                    break;
                case "TextBox":
                case "AutoCompleteTagger":
                    ((TextBox)ActiveControl).Copy();
                    break;
                case "ComboBox":
                    Clipboard.SetText(((ComboBox)ActiveControl).SelectedText);
                    break;
            }
        }

        private void MnTx_Paste_Click(object sender, EventArgs e)
        {
            string sAdd = Clipboard.GetText();
            switch (ActiveControl.GetType().Name) {
                case "FixedRichTextBox":
                    FixedRichTextBox fr = ((FixedRichTextBox)ActiveControl);
                    fr.SelectedText = sAdd;
                    if (TabControl.SelectedIndex == 2) bSavText = false;
                    break;
                case "TextBox":
                case "AutoCompleteTagger":
                    if(ActiveControl.Name == "TxBx_Search") {
                        string[] asTitle = SplitTitle(sAdd);
                        sAdd = (asTitle.Length < 2) ? sAdd : string.Format("artist:{0} title:{1}",
                            asTitle[0].Replace(' ', '_'), asTitle[1].Replace(' ', '_'));
                        TxBx_Search.SelectionStart = InsertText(
                            TxBx_Search, sAdd, TxBx_Search.SelectionStart);
                        Search();
                        break;
                    }

                    TextBox txt = (TextBox)ActiveControl;
                    if (txt.Name == "acTxBx_Tags" && sAdd.Contains("\r")) {
                        IEnumerable<string> ie = sAdd.Split('(', '\n');
                        ie = ie.Where(s => !s.EndsWith("\r") 
                            && !s.EndsWith(")") && !s.Equals(""));
                        ie = ie.Select(s => s.TrimEnd());
                        sAdd = string.Join(", ", ie.ToArray());
                    }
                    txt.SelectedText = sAdd;
                    break;
                case "ComboBox":
                    ComboBox cb = (ComboBox)ActiveControl;
                    
                    if(sAdd.Contains('[')) SplitTitleSet(SplitTitle(sAdd));
                    else cb.SelectedText = sAdd;
                    break;
            }
        }

        private void MnTx_Delete_Click(object sender, EventArgs e)
        {
            switch (ActiveControl.GetType().Name) {
                case "FixedRichTextBox":
                    ((FixedRichTextBox)ActiveControl).SelectedText = "";
                    break;
                case "TextBox":
                case "AutoCompleteTagger":
                    ((TextBox)ActiveControl).SelectedText = "";
                    break;
                case "ComboBox":
                    ((ComboBox)ActiveControl).SelectedText = "";
                    break;
            }
        }

        private void MnTx_SelAll_Click(object sender, EventArgs e)
        {
            switch (ActiveControl.GetType().Name) {
                case "FixedRichTextBox":
                    ((FixedRichTextBox)ActiveControl).SelectAll();
                    break;
                case "TextBox":
                case "AutoCompleteTagger":
                    ((TextBox)ActiveControl).SelectAll();
                    break;
                case "ComboBox":
                    ((ComboBox)ActiveControl).SelectAll();
                    break;
            }
        }

        private void Mn_TxBx_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool bUndo = false, bSelect = false;
            switch (ActiveControl.GetType().Name) {
                case "FixedRichTextBox":
                    FixedRichTextBox fr = ActiveControl as FixedRichTextBox;
                    if (fr.CanUndo) bUndo = true;
                    if (fr.SelectionLength > 0) bSelect = true;
                    break;
                case "TextBox":
                case "AutoCompleteTagger":
                    TextBox txt = ActiveControl as TextBox;
                    if (txt.CanUndo) bUndo = true;
                    if (txt.SelectionLength > 0) bSelect = true;
                    break;
                case "ComboBox":
                    ComboBox cb = ActiveControl as ComboBox;
                    if (cb.SelectionLength > 0) bSelect = true;
                    bUndo = true;
                    break;
            }

            MnTx_Undo.Enabled = bUndo;
            MnTx_Cut.Enabled = bSelect;
            MnTx_Copy.Enabled = bSelect;
            MnTx_Delete.Enabled = bSelect;
        }

        /* Ensure element recieves focus when right-clicked */
        private void TbPg_Click(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            Control cnt = sender as Control;
            if (cnt.CanSelect) cnt.Select();
        }

        private void frTxBx_KeyDown(object sender, KeyEventArgs e)
        {
            /* Prevent mixed font types when c/p  */
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.V) {
                FixedRichTextBox frTxBx = sender as FixedRichTextBox;
                frTxBx.SelectionStart = InsertText(
                    frTxBx, Clipboard.GetText(), frTxBx.SelectionStart);
                if (TabControl.SelectedIndex == 2) bSavText = false;
                e.Handled = true;
            }
            /* Prevent console beep when reaching start/end of txbx */
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Left) {
                FixedRichTextBox frTxBx = sender as FixedRichTextBox;
                if ((e.KeyCode == Keys.Right && frTxBx.SelectionStart == frTxBx.TextLength) ||
                    (e.KeyCode == Keys.Left && frTxBx.SelectionStart == 0))
                    e.Handled = true;
            }
        }
        #endregion

        #region DragDropEvents
        /* Show proper cursor when text is dragged over TxBx */
        private void DragEnterTxBx(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text))
                e.Effect = DragDropEffects.Copy;
            else e.Effect = DragDropEffects.None;
        }

        /* Parse dropped text into TxBx(s) */
        private void DragDropTxBx(object sender, DragEventArgs e)
        {
            string sAdd = (string)e.Data.GetData(DataFormats.Text);

            switch ((sender as Control).Name) {
                case "frTxBx_Desc":
                    frTxBx_Desc.SelectionStart = InsertText(
                        frTxBx_Desc, sAdd, frTxBx_Desc.SelectionStart);
                    break;
                case "frTxBx_Notes":
                    frTxBx_Notes.SelectionStart = InsertText(
                        frTxBx_Notes, sAdd, frTxBx_Notes.SelectionStart);
                    break;
                case "CmbBx_Artist":
                    if (sAdd.Contains('[')) SplitTitleSet(SplitTitle(sAdd));
                    else CmbBx_Artist.SelectionStart = InsertText(
                        CmbBx_Artist, sAdd, CmbBx_Artist.SelectionStart);
                    break;
                case "TxBx_Title":
                    if (sAdd.Contains('[')) SplitTitleSet(SplitTitle(sAdd));
                    else TxBx_Title.SelectionStart = InsertText(
                        TxBx_Title, sAdd, TxBx_Title.SelectionStart);
                    break;
                case "acTxBx_Tags":
                    if (sAdd.Contains("\r")) {
                        IEnumerable<string> ie = sAdd.Split('(', '\n');
                        ie = ie.Where(s => !s.EndsWith("\r") && !s.EndsWith(")") && !s.Equals(""));
                        ie = ie.Select(s => s.TrimEnd());
                        sAdd = string.Join(", ", ie.ToArray());
                    }
                    acTxBx_Tags.SelectionStart = InsertText(
                        acTxBx_Tags, sAdd, acTxBx_Tags.SelectionStart);
                    break;
                case "TxBx_Search":
                    string[] asTitle = SplitTitle(sAdd);
                    sAdd = (asTitle.Length < 2) ? sAdd : string.Format("artist:{0} title:{1}", 
                        asTitle[0].Replace(' ', '_'), asTitle[1].Replace(' ', '_'));
                    TxBx_Search.SelectionStart = InsertText(
                        TxBx_Search, sAdd, TxBx_Search.SelectionStart);
                    Search();
                    break;
            }
        }

        private void TxBx_Loc_DragDrop(object sender, DragEventArgs e)
        {
            string[] asDir = ((string[])e.Data.GetData(DataFormats.FileDrop, false));
            TxBx_Loc.Text = asDir[0];

            if(Directory.Exists(asDir[0])) {
                if (CmbBx_Artist.Text == "" && TxBx_Title.Text == "") {
                    SplitTitleSet(SplitTitle(Path.GetDirectoryName(asDir[0])));
                    ThreadPool.QueueUserWorkItem(GetImage);
                }
            }
            else if (File.Exists(asDir[0]) && IsArchive(asDir[0])) {
                if (CmbBx_Artist.Text == "" && TxBx_Title.Text == "") {
                    SplitTitleSet(SplitTitle(Path.GetFileNameWithoutExtension(asDir[0])));
                    ThreadPool.QueueUserWorkItem(GetImage);
                }
            }
        }

        private void Dt_Date_DragDrop(object sender, DragEventArgs e)
        {
            DateTime dtDrop;
            if (DateTime.TryParse(
                    (string)e.Data.GetData(DataFormats.Text),
                    out dtDrop)) {
                Dt_Date.Value = dtDrop;
            }
            else MessageBox.Show("The dropped text was not a valid date.",
                Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
        private void Nud_Pages_DragDrop(object sender, DragEventArgs e)
        {
            decimal dcDrop;
            if (decimal.TryParse(
                    (string)e.Data.GetData(DataFormats.Text), 
                    out dcDrop)) {
                Nud_Pages.Value = dcDrop;
            }
            else MessageBox.Show("The dropped text was not a valid value.",
                Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        /* Allow dropping of folders/zips onto LV_Entries (& TxBx_Loc) */
        private void LV_Entries_DragEnter(object sender, DragEventArgs e)
        {
            string[] sTemp = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            if (sTemp == null) return;
            
            FileAttributes fa = File.GetAttributes(sTemp[0]);Text = fa.ToString();
            if (fa == FileAttributes.Directory || IsArchive(sTemp[0])
                    || fa.ToString() == "Directory, Archive")
                e.Effect = DragDropEffects.Copy;
            else e.Effect = DragDropEffects.None;
        }

        /* Adds folder(s) to database when dragged over LV_Entries */
        private void LV_Entries_DragDrop(object sender, DragEventArgs e)
        {
            string[] asDir = ((string[])e.Data.GetData(DataFormats.FileDrop, false));
            short iTrack = indx;
            string sError = "";

            //add all remaining folders
            for (int i = 0; i < asDir.Length; i++) {
                if (asDir[i] == "") continue;

                //add item
                Main.csEntry en = new csEntry(asDir[i]);
                if (!lData.Contains(en)) {
                    lData.Add(en);
                    indx = (short)(lData.Count - 1);

                    if (en.sArtist != "" && !CmbBx_Artist.Items.Contains(en.sArtist))
                        CmbBx_Artist.Items.Add(en.sArtist);
                }
                else sError += '\n' + asDir[i];
            }
            if (sError != "") {
                MessageBox.Show("The following path(s) already exists in the database:" + sError,
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //Update LV
            if (iTrack != indx)
                AddEntries();
        }
        #endregion

        #region Cruft
        /* Legacy Manga entry code 
           Used for converting old databases */
        [Serializable]
        public class stEntry : ISerializable
        {
            public string sTitle, sArtist,
                sLoc, sType, sDesc, sTags;
            public DateTime dtDate;
            public ushort iPages;
            public bool bFav;

            public stEntry(string _Title, string _Artist, string _Loc, string _Desc,
                string _Tags, string _Type, DateTime _Date, decimal _Pages, bool _Fav)
            {
                sTitle = _Title;
                sArtist = _Artist;
                sLoc = _Loc;
                sDesc = _Desc;
                dtDate = _Date;
                iPages = (ushort)_Pages;
                sType = _Type;
                bFav = _Fav;
                sTags = _Tags;
            }

            /* custom serialization to save datatypes manually */
            protected stEntry(SerializationInfo info, StreamingContext ctxt)
            {
                sTitle = info.GetString("Title");
                sArtist = info.GetString("Artist");
                sLoc = info.GetString("Loc");
                sDesc = info.GetString("Desc");
                dtDate = info.GetDateTime("Date");
                sType = info.GetString("Type");
                bFav = info.GetBoolean("Fav");
                sTags = info.GetString("Tags");
                iPages = (ushort)info.GetInt32("Pages");
            }

            /* custom serialization to read datatypes manually */
            [System.Security.Permissions.SecurityPermission(System.Security.Permissions.
                SecurityAction.LinkDemand, Flags = System.Security.Permissions.
                SecurityPermissionFlag.SerializationFormatter)]
            public virtual void GetObjectData(SerializationInfo info, StreamingContext ctxt)
            {
                info.AddValue("Title", sTitle);
                info.AddValue("Artist", sArtist);
                info.AddValue("Loc", sLoc);
                info.AddValue("Desc", sDesc);
                info.AddValue("Date", dtDate);
                info.AddValue("Pages", iPages);
                info.AddValue("Type", sType);
                info.AddValue("Fav", bFav);
                info.AddValue("Tags", sTags);
            }
        }
        #endregion
    }
}