using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;

namespace Nagru___Manga_Organizer
{
  public static class Ext
  {
    /// <summary>
    /// Ensure chosen folder is not protected before operating
    /// </summary>
    /// <param name="Path"></param>
    /// <returns></returns>
    public static bool Accessible(string Path)
    {
      if (!Directory.Exists(Path))
        return false;

      try {
        string[] asDirs = Directory.GetDirectories(Path, "*",
            SearchOption.TopDirectoryOnly);
        FileIOPermission fp;

        for (int i = 0; i < asDirs.Length; i++) {
          fp = new FileIOPermission(FileIOPermissionAccess.Read |
              FileIOPermissionAccess.Write, asDirs[i]);
          fp.Demand();
        }
      } catch {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Simple string comparison
    /// </summary>
    /// <param name="sRaw">The base string</param>
    /// <param name="sFind">The string to search for</param>
    /// <param name="cComp">Overrideable comparison type</param>
    /// <returns>Returns true if sRaw contains sFind</returns>
    public static bool Contains(this string sRaw, string sFind,
      StringComparison cComp = StringComparison.OrdinalIgnoreCase)
    {
      return sRaw.IndexOf(sFind, cComp) > -1;
    }

    /// <summary>
    /// Predict the filepath of a manga
    /// </summary>
    /// <param name="sArtist">The name of the Artist</param>
    /// <param name="sTitle">The title of the manga</param>
    /// <param name="sMangaLocation">The base filepath</param>
    /// <returns></returns>
    public static string FindPath(string sArtist, string sTitle, string sMangaLocation = null)
    {
      if (!File.Exists(sMangaLocation) && !Directory.Exists(sMangaLocation)) {
        string SQLSetting = SQL.GetSetting(SQL.Setting.RootPath);
        string RootPath = !string.IsNullOrWhiteSpace(SQLSetting) ? SQLSetting : Environment.CurrentDirectory;

        sMangaLocation = Ext.CorrectPath(
          string.Format("{0}\\{1}", RootPath, Ext.GetFormattedTitle(sArtist, sTitle))
          , RootPath
        );

        if (!Directory.Exists(sMangaLocation) && !File.Exists(sMangaLocation)) {
          sMangaLocation = null;
        }
      }

      return sMangaLocation;
    }

    /// <summary>
    /// Find if an existing file/directory matches the passed in path
    /// </summary>
    /// <param name="Source">The base filepath</param>
    /// <param name="RootPath">Override for where to find possible matches</param>
    /// <returns></returns>
    public static string CorrectPath(string Source, string RootPath = null)
    {
      if (!File.Exists(Source) && !Directory.Exists(Source)) {
        const double MinSimilarity = 0.8;
        List<string> lRootDirs = new List<string>(500);
        if (string.IsNullOrWhiteSpace(RootPath)) {
          string SQLSetting = Convert.ToString(SQL.GetSetting(SQL.Setting.RootPath));
          RootPath = !string.IsNullOrWhiteSpace(SQLSetting) ? SQLSetting : Environment.CurrentDirectory;
        }

        if (Directory.Exists(RootPath)) {
          string[] validTypes = new string[5] { ".cbz", ".cbr", ".zip", ".rar", ".7z" };
          foreach (string path in Directory.EnumerateDirectories(RootPath)) {
            if (SoerensonDiceCoef(Source, path) > MinSimilarity) {
              Source = path;
              break;
            }
          }
          foreach (string path in Directory.EnumerateFiles(RootPath)) {
            if (validTypes.Contains(Path.GetExtension(path))) {
              if (SoerensonDiceCoef(Source, path) > MinSimilarity) {
                Source = path;
                break;
              }
            }
          }
        }
      }

      return Source;
    }

    /// <summary>
    /// Extends Directory.GetFiles to support multiple filters
    /// </summary>
    /// <remarks>Inspiration: Bean Software (2002-2008)</remarks>
    /// <param name="SourceFolder"></param>
    /// <param name="SearchOption"></param>
    /// <param name="Filter"></param>
    /// <returns></returns>
    public static string[] GetFiles(string SourceFolder,
        SearchOption SearchOption = SearchOption.AllDirectories,
        string Filter = "*.jpg|*.jpeg|*.png|*.gif")
    {
      if (!Directory.Exists(SourceFolder))
        return new string[0];
      List<string> lFiles = new List<string>(10000);
      string[] sFilters = Filter.Split('|');

      try {
        for (int i = 0; i < sFilters.Length; i++) {
          lFiles.AddRange(Directory.GetFiles(SourceFolder,
          sFilters[i], SearchOption));
        }
      } catch (ArgumentException) {
        Console.WriteLine("Invalid characters in path:\n" + SourceFolder);
      } catch (UnauthorizedAccessException) {
        Console.WriteLine("User does not have access to:\n" + SourceFolder);
      } catch (Exception ex) {
        Console.WriteLine(ex.Message);
      }

      lFiles.Sort(new TrueCompare());
      return lFiles.ToArray();
    }

    /// <summary>
    /// Turns Artist and Title fields into their EH format
    /// </summary>
    /// <param name="Artist"></param>
    /// <param name="Title"></param>
    /// <returns></returns>
    public static string GetFormattedTitle(string Artist, string Title)
    {
      return string.Format((!string.IsNullOrWhiteSpace(Artist))
          ? "[{0}] {1}" : "{1}", Artist, Title);
    }

    /// <summary>
    /// Return a filename without its extension
    /// Overcomes Microsoft not handling periods in filenames
    /// </summary>
    /// <param name="sName"></param>
    /// <returns></returns>
    public static string GetNameSansExtension(string sName)
    {
      StringBuilder sb;
      if (Directory.Exists(sName)) {
        sb = new StringBuilder(Path.GetFileName(sName));
      }
      else {
        sb = new StringBuilder(sName);
        int indx = sName.LastIndexOf('\\');

        if (indx > -1) {
          sb.Remove(0, indx + 1);
        }

        indx = sb.ToString().LastIndexOf('.');
        if (indx > -1) {
          sb.Remove(indx, sb.Length - indx);
        }
      }

      return sb.ToString();
    }

    /// <summary>
    /// Ensures the SQL class is initialized
    /// </summary>
    /// <returns>Whether the SQL class is accessible. Safety check.</returns>
    public static bool IsInitialized()
    {
      bool bInitialized = true;

      try {
        SQL.IsConnected();
      } catch (System.TypeInitializationException) {
        bInitialized = false;
      }

      return bInitialized;
    }

    /// <summary>
    /// Adds text to a control
    /// </summary>
    /// <param name="c">The control to alter</param>
    /// <param name="sAdd">The text to add</param>
    /// <param name="iStart">The start point to insert from</param>
    /// <returns></returns>
    public static int InsertText(System.Windows.Forms.Control c, string sAdd, int iStart)
    {
      c.Text = c.Text.Insert(iStart, sAdd);
      return iStart + sAdd.Length;
    }

    /// <summary>
    /// Parses the input string into Artist and Title variables
    /// </summary>
    /// <param name="sRaw">The string to parse</param>
    /// <returns>Returns the Artist [0] and Title [1]</returns>
    public static string[] ParseGalleryTitle(string sRaw)
    {
      string[] asName = new string[2] { "", "" };
      string sCircle = "";
      int iPos = -1;

      if (!string.IsNullOrWhiteSpace(sRaw)) {
        //strip out circle info & store
        if (sRaw.StartsWith("(")) {
          iPos = sRaw.IndexOf(')');
          if (iPos > -1 && ++iPos < sRaw.Length) {
            sCircle = sRaw.Substring(0, iPos);
            sRaw = sRaw.Remove(0, iPos).TrimStart();
          }
        }

        //split fields using EH format
        int iA = sRaw.IndexOf('['), iB = sRaw.IndexOf(']');
        if (iPos != 0																						//ensure '(circle) [name]~' or '[name]~' format
            && iA == 0 && iB > -1                               //ensure there's a closing brace
            && iA < iB                                          //ensure the closing brace comes *after*
            && ++iB < sRaw.Length)                              //ensure there is text after the brace
        {
          //Re-format for Artist/Title fields
          asName[0] = sRaw.Substring(iA + 1, iB - iA - 2).Trim();
          if (!string.IsNullOrWhiteSpace(sCircle)) {
            asName[1] = sRaw.Substring(iB).Trim() + " " + sCircle;
          }
          else {
            asName[1] = sRaw.Substring(iB).Trim();
          }
        }
        else {
          asName[1] = sRaw;
        }
      }
      return asName;
    }

    /// <summary>
    /// Convert number to string of stars
    /// </summary>
    /// <param name="iRating"></param>
    public static string RatingFormat(int iRating)
    {
      return string.Format("{0}{1}"
        , new string('★', iRating)
        , iRating != 5 ? new string('☆', 5 - iRating) : ""
      );
    }

    /// <summary>
    /// Proper image scaling
    /// </summary>
    /// <remarks>based on: Alex Aza (Jun 28, 2011)</remarks>
    /// <param name="img"></param>
    /// <param name="fMaxWidth"></param>
    /// <param name="fMaxHeight"></param>
    /// <returns></returns>
    public static Bitmap ScaleImage(Image img, float fMaxWidth, float fMaxHeight)
    {
        int iWidth = img.Width;
        int iHeight = img.Height;

        float fRatio = Math.Min(
        fMaxWidth / img.Width,
        fMaxHeight / img.Height);

        iWidth = (int)(img.Width * fRatio);
        iHeight = (int)(img.Height * fRatio);

        Bitmap bmpNew = new Bitmap(iWidth, iHeight);
        Graphics.FromImage(bmpNew).DrawImage(img, 0, 0, iWidth, iHeight);
        return bmpNew;
    }

    /// <summary>
    /// Finds the value of divergence between two strings
    /// </summary>
    /// <param name="sA"></param>
    /// <param name="sB"></param>
    /// <param name="bIgnoreCase"></param>
    /// <returns></returns>
    public static double SoerensonDiceCoef(string sA, string sB, bool bIgnoreCase = true)
    {
      HashSet<string> hsA = new HashSet<string>(),
          hsB = new HashSet<string>();

      if (bIgnoreCase) {
        sA = sA.ToLower();
        sB = sB.ToLower();
      }

      //create paired char chunks from strings to compare
      for (int i = 0; i < sA.Length - 1; ) {
        hsA.Add(sA[i] + "" + sA[++i]);
      }
      for (int i = 0; i < sB.Length - 1; ) {
        hsB.Add(sB[i] + "" + sB[++i]);
      }
      int iTotalElements = hsA.Count + hsB.Count;

      hsA.IntersectWith(hsB);
      return (double)(2 * hsA.Count) / iTotalElements;
    }

    /// <summary>
    /// Splits string using multiple filter terms
    /// Also removes empty entries from the results
    /// </summary>
    /// <param name="sRaw"></param>
    /// <param name="sFilter"></param>
    /// <returns></returns>
    public static string[] Split(string sRaw, params string[] sFilter)
    {
      return sRaw.Split(sFilter, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim()).ToArray<string>();
    }
  }
}