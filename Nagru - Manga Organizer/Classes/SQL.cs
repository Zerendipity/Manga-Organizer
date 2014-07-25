﻿using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Nagru___Manga_Organizer
{
	/// <summary>
	/// Controls access to the database
	/// </summary>
  public static class SQL
  {
    #region Properties

		public delegate void DelVoidInt(int i);
    public static DelVoidInt delProgress = null;
		private static SQLBase sqlBase = null;
		
		#region Table Enums

		public enum Setting {
      DBversion
			,RootPath
			,SavePath
			,SearchIgnore
			,FormPosition
			,ImageBrowser
      ,Notes
			,member_id
			,pass_hash
			,NewUser
			,SendReports
			,ShowGrid
			,ShowDate
			,ReadInterval
      ,RowColourHighlight
			,RowColourAlt
			,BackgroundColour
			,GallerySettings
			,CreatedDBTime
			,AuditDBTime
    };

    public enum Manga {
      MangaID
      ,Artist
      ,Title
      ,Pages
      ,Tags
      ,Description
      ,PublishedDate
      ,Location
      ,GalleryURL
      ,Thumbnail
      ,Type
      ,Rating
    };

		#endregion// prevents having to write alter statements

		#region Views

			internal const string vsManga = @"
				select
					mgx.MangaID
					,ifnull(at.Name, '')    Artist
					,mgx.Title
					,mgx.Pages
					,tg.Tags
					,mgx.Description
					,mgx.PublishedDate
					,mgx.Location
					,mgx.GalleryURL
          ,mgx.Thumbnail
					,ifnull(tp.Type, '')    Type
					,mgx.Rating
				from
					[Manga] mgx
				left outer join
					[Type] tp on tp.TypeID = mgx.TypeID
				left outer join
					[MangaArtist] mga on mga.MangaID = mgx.MangaID
				left outer join
					[Artist] at on at.ArtistID = mga.ArtistID
				left outer join
				(
					select MangaID, group_concat(Tag, ', ') Tags
					from 
					(
						select mgt.MangaID, mgt.TagID, tx.Tag
						from [Tag] tx
						join [MangaTag] mgt on mgt.TagID = tx.TagID
						order by tx.Tag
					)
					group by MangaID
				) tg on tg.MangaID = mgx.MangaID ";
			internal const string vsMangaEnd = " group by mgx.MangaID ";

			#endregion

    #endregion

    #region Constructor

		/// <summary>
		/// Instantiates the non-static sqlBase that holds the DB connection
		/// </summary>
    static SQL()
    {
			sqlBase = new SQLBase();
			sqlBase.UpdateVersion();
    }

    #endregion

    #region Public Access

    #region Connection

    /// <summary>
    /// Opens a connection to the database and imports any previous Manga DB's if possible
    /// </summary>
    /// <param name="_filePath">The path to the old database</param>
    /// <returns>Returns whether the operation succeeded or failed</returns>
    public static bool Connect(string _filePath = null)
    {
      if (string.IsNullOrEmpty(_filePath)) {
        _filePath = SQL.GetSetting(SQL.Setting.SavePath) != string.Empty ?
            SQL.GetSetting(SQL.Setting.SavePath) : Environment.CurrentDirectory;
        _filePath += "\\MangaDatabase.bin";
      }

      if (File.Exists(_filePath)
          || File.Exists(_filePath = Ext.RelativePath(_filePath))) {
        sqlBase.Import(_filePath);
      }

			return IsConnected();
    }

		/// <summary>
		/// Returns whether or not the DB connection is currently open
		/// </summary>
		public static bool IsConnected()
		{
			return (sqlBase != null && sqlBase.sqConn != null
				&& sqlBase.sqConn.State == ConnectionState.Open);
		}

    /// <summary>
    /// Vacuum's the DB and closes the connection
    /// </summary>
    public static void Disconnect()
    {
      sqlBase.Close();
			sqlBase.Dispose();
    }

    #endregion

    #region Query Database

    /// <summary>
    /// Returns all the artists in the database
    /// </summary>
    public static string[] GetArtists()
    {
			string[] asArtists;
			using (DataTable dtArtists = SQLAccess.GetArtists())
			{
				asArtists = new string[dtArtists.Rows.Count];
				for (int i = 0; i < dtArtists.Rows.Count; i++)
				{
					asArtists[i] = dtArtists.Rows[i]["Artist"].ToString();
        }
			}
			return asArtists;
    }

    /// <summary>
    /// Returns all the manga types in the database
    /// </summary>
    public static string[] GetTypes()
    {
			string[] asTypes;
			using (DataTable dtTypes = SQLAccess.GetTypes())
			{
				asTypes = new string[dtTypes.Rows.Count];
				for (int i = 0; i < dtTypes.Rows.Count; i++)
				{
					asTypes[i] = dtTypes.Rows[i]["Type"].ToString();
				}
			}
			return asTypes;
    }

    /// <summary>
    /// Returns all the tags in the database
    /// </summary>
    public static string[] GetTags()
    {
			string[] asTags;
			using (DataTable dtTags = SQLAccess.GetTags())
			{
				asTags = new string[dtTags.Rows.Count];
				for (int i = 0; i < dtTags.Rows.Count; i++)
				{
					asTags[i] = dtTags.Rows[i]["Tag"].ToString();
				}
			}
			return asTags;
    }

    /// <summary>
    /// Returns the EH formatted title of a Manga
    /// </summary>
    /// <param name="mangaID">The ID of the record to access</param>
    public static string GetMangaTitle(int mangaID)
    {
			string sTitle = string.Empty;
			using (DataTable dt = SQLAccess.DB_GetEntryDetails(mangaID))
			{
				sTitle = Ext.GetFormattedTitle(
					dt.Rows[0]["Artist"].ToString(),
					dt.Rows[0]["Title"].ToString()
				);
      }

      return sTitle;
    }

    /// <summary>
    /// Returns the full details of a specified manga
    /// </summary>
    /// <param name="mangaID">The ID of the record</param>
    public static DataTable GetManga(int mangaID)
    {
			return SQLAccess.DB_GetEntryDetails(mangaID);
    }

    /// <summary>
    /// Returns a single detail of a specified manga
    /// </summary>
    /// <param name="mangaID">The ID of the record</param>
    /// <param name="columnName">The name of the column to extract</param>
    public static string GetMangaDetail(int mangaID, Manga Column)
    {
      string sVal = "";
			using (DataTable dt = SQLAccess.DB_GetEntryDetails(mangaID))
			{
        sVal = dt.Rows[0][Column.ToString()].ToString();
      }
      return sVal;
    }

    /// <summary>
    /// Returns a setting from the DB
    /// </summary>
    /// <param name="DBSetting"></param>
    public static string GetSetting(Setting DBSetting)
    {
      string sVal = "";
			using (DataTable dt = SQLAccess.DB_GetSettings())
			{
        sVal = dt.Rows[0][DBSetting.ToString()].ToString();
      }
      return sVal;
    }

    /// <summary>
    /// Returns the details of every manga in the database
    /// </summary>
    /// <param name="OnlyFavourites">Only return entries with a rating of 5.0?</param>
		public static DataTable GetAllEntries(bool OnlyFavourites = false)
    {
			return SQLAccess.GetEntries(OnlyFavourites);
    }

    #region Search Database

    /// <summary>
    /// Returns whether the entry exists in the DB
    /// </summary>
    /// <param name="Artist">The artist's name</param>
    /// <param name="Title">The title of the maga</param>
    public static bool ContainsEntry(string Artist, string Title)
    {
			return SQLAccess.EntryExists(Artist, Title);
    }

    /// <summary>
    /// Uses EH-like parameters to search the DB for matching manga
    /// </summary>
    /// <param name="SearchTerms">The raw search terms</param>
		/// <param name="OnlyFavourites">Only return entries with a rating of 5.0?</param>
    /// <param name="MangaID">Check a specific manga to see if it matches the conditions</param>
		public static DataTable Search(string SearchTerms, bool OnlyFavourites = false, int MangaID = -1)
    {
			return SQLAccess.DB_Search(SearchTerms, OnlyFavourites, MangaID);
    }

    #endregion

    #endregion

    #region Update Database

    /// <summary>
    /// Insert or updates a manga
    /// </summary>
    /// <param name="Artist">The name of the artist</param>
    /// <param name="Title">The title of the manga</param>
    /// <param name="Tags">The comma-delimited tags</param>
    /// <param name="Location">The local filepath</param>
    /// <param name="PublishedDate">The date the manga was published to EH</param>
    /// <param name="Pages">The number of pages</param>
    /// <param name="Type">The type of the manga</param>
    /// <param name="Rating">The decimal rating</param>
    /// <param name="Description">User comments</param>
    /// <param name="URL">The source URL of the gallery</param>
    /// <param name="MangaID">If passed through, attempts to update the indicated record</param>
    public static int SaveManga(string Artist, string Title, DateTime PublishedDate,
        string Tags = null, string Location = null, decimal Pages = 0, string Type = null, 
        decimal Rating = 0, string Description = null, string URL = null, int MangaID = -1)
    {
			return SQLAccess.DB_SaveEntry(Artist, Title, PublishedDate, Tags, 
        Location, Pages, Type, Rating, Description, URL, MangaID);
    }

    /// <summary>
    /// Deletes an entry from the database
    /// </summary>
    /// <param name="mangaID">The ID of the record to be deleted</param>
    public static bool DeleteManga(int mangaID)
    {
			int altered = SQLAccess.Entry_Delete(mangaID);
      return (altered == 1);
    }

    /// <summary>
    /// Deletes all unused tags from the DB
    /// </summary>
    /// <returns>Returns the number of deleted tags.</returns>
    public static int CleanUpTags()
    {
			return SQLAccess.DeleteUnusedTags();
    }

    /// <summary>
    /// Updates only the rating of the indicated record
    /// </summary>
    /// <param name="MangaID">The ID of the record to update</param>
    /// <param name="Rating">The new rating value</param>
    public static void UpdateRating(int MangaID, decimal Rating)
    {
      string sCommandText = "update Manga set Rating = @rating where MangaID = @mangaID";

      SQLiteParameter sqManga = SQLBase.NewParameter("@mangaID", DbType.Int32, MangaID);
			SQLiteParameter sqRating = SQLBase.NewParameter("@rating", DbType.Decimal, Rating);

      sqlBase.ExecuteNonQuery(sCommandText, CommandBehavior.Default, sqManga, sqRating);
    }

    /// <summary>
    /// Update the indicated setting
    /// </summary>
    /// <param name="DBSetting">The name of the setting to update</param>
    /// <param name="setting">The new value</param>
    public static void UpdateSetting(Setting DBSetting, object setting)
    {
			SQLAccess.DB_UpdateSetting(DBSetting, setting);
    }

    #endregion

    #endregion

		private class SQLBase : IDisposable
		{
			#region Properties

			internal SQLiteConnection sqConn = null;
			private const int SQLITE_MAX_LENGTH = 1000000;
			private const int DB_VERSION = 1;
			private bool bDisposed = false;

			#endregion

			#region Constructor

			/// <summary>
			/// Establish a DB connection when instantiated
			/// </summary>
			internal SQLBase()
			{
				this.Connect();
			}

			/// <summary>
			/// Public implementation of Dispose
			/// </summary>
			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			//protected implementation of Dispose
			protected virtual void Dispose(bool Disposing)
			{
				if (bDisposed)
					return;

				if (Disposing)
				{
					sqConn.Dispose();
				}

				bDisposed = true;
			}

			/// <summary>
			/// Destructor
			/// </summary>
			~SQLBase()
			{
				Dispose(false);
			}

			#endregion

			#region Handle connection

			/// <summary>
			/// Establishes a connection with the database or, if one is not found, create a new instance
			/// </summary>
			internal void Connect()
			{
				Close();
				string sPath = Properties.Settings.Default.SavLoc != string.Empty ?
					Properties.Settings.Default.SavLoc : Environment.CurrentDirectory;
				sPath += "\\MangaDB.sqlite";

				//check existence
				bool bExist = File.Exists(sPath);

				//create connection
				sqConn = new SQLiteConnection();
				if (!bExist)
					SQLiteConnection.CreateFile(sPath);
				sqConn.ConnectionString = new DbConnectionStringBuilder()
				{
					{"Data Source", sPath},
					{"Version", "3"},
					{"Compress", true},
					{"New", !bExist}
				}.ConnectionString;
				sqConn.Open();

				if (!bExist)
				{
					CreateDatabase();
				}
			}

			/// <summary>
			/// Check if there are updates to the DB, and if so deploy them
			/// </summary>
			internal void UpdateVersion()
			{
				int iCurrentVersion = 0;

				//check if there's a new version of the database
				using (DataTable dt = ExecuteQuery("select * from sqlite_master where tbl_name = 'Settings'"))
				{
					if (dt.Rows.Count > 0)
					{
						iCurrentVersion = Int32.Parse(GetSetting(Setting.DBversion));
					}
				}
				if (DB_VERSION == iCurrentVersion)
					return;

				switch (iCurrentVersion)
				{
					default:
					case 0:
						#region Update to version 1.0
						BeginTransaction();
						Create_Settings();

						#region Grab the current settings and populate the table
						List<SQLiteParameter> sqParam = new List<SQLiteParameter>(20);

						string sQuery = @"
							update [Settings]
							set DBversion           = 1
							,RootPath							= @rootPath
							,SavePath							= @savePath
							,SearchIgnore					= @ignore
							,FormPosition					= @position
							,ImageBrowser					= @browser
							,Notes                = @notes
							,member_id						= @memberID
							,pass_hash						= @passHash
							,NewUser							= 0
							,SendReports					= @sendReports
							,ShowGrid							= @showGrid
							,ShowDate							= @showDate
							,ReadInterval					= @interval
							,RowColourHighlight		= @rowHighlight
							,RowColourAlt					= @rowAlt
							,BackgroundColour			= @background
							,GallerySettings			= @galleries";

						sqParam.AddRange(new SQLiteParameter[14]{
							NewParameter("@rootPath", DbType.String, Properties.Settings.Default.DefLoc),
							NewParameter("@savePath", DbType.String, Properties.Settings.Default.SavLoc),
							NewParameter("@ignore", DbType.String, Properties.Settings.Default.Ignore),
							NewParameter("@browser", DbType.String, Properties.Settings.Default.DefProg),
							NewParameter("@notes", DbType.String, Properties.Settings.Default.Notes),
							NewParameter("@passHash", DbType.String, Properties.Settings.Default.pass_hash),
							NewParameter("@sendReports", DbType.Int32, Properties.Settings.Default.SendReports ? 1 : 0),
							NewParameter("@showGrid", DbType.Int32, Properties.Settings.Default.DefGrid ? 1 : 0),
							NewParameter("@showDate", DbType.Int32, Properties.Settings.Default.HideDate ? 0 : 1),
							NewParameter("@interval", DbType.Int32, Properties.Settings.Default.Interval),
							NewParameter("@rowHighlight", DbType.Int32, Properties.Settings.Default.RowColorHighlight),
							NewParameter("@rowAlt", DbType.Int32, Properties.Settings.Default.RowColorAlt),
							NewParameter("@background", DbType.Int32, Properties.Settings.Default.DefColour.ToArgb()),
							NewParameter("@galleries", DbType.String, Properties.Settings.Default.GalleryTypes),
						});
						sqParam.Add(NewParameter("@position", DbType.String, 
							string.Format("{0},{1},{2},{3}"
								, Properties.Settings.Default.Position.X
								, Properties.Settings.Default.Position.Y
								, Properties.Settings.Default.Position.Width
								, Properties.Settings.Default.Position.Height)
						));
						sqParam.Add(NewParameter("@memberID", DbType.Int32, 
							!string.IsNullOrEmpty(Properties.Settings.Default.member_id) ?
								Int32.Parse(Properties.Settings.Default.member_id) : -1)
						);

						ExecuteNonQuery(sQuery, CommandBehavior.Default, sqParam.ToArray());
						#endregion

						#region Add the Thumbnails column to dbo.Manga
						sQuery = @"
					alter table [Manga]
					add column Thumbnail blob null";
						ExecuteNonQuery(sQuery);
						#endregion

						#region Remove the Audit details from the link tables
						sQuery = @"
					drop trigger trMangaArtist;
					create temporary table [tmpMangaArtist](MangaID, ArtistID);
					insert into [tmpMangaArtist] select MangaID, ArtistID from [MangaArtist];
					drop table [MangaArtist];
        
					create table [MangaArtist]
					(
						MangaArtistID		integer		primary key		autoincrement
						,MangaID				integer		not null
						,ArtistID				integer		not null
						,constraint [fk_mangaID] foreign key ([MangaID]) references [Manga] ([MangaID])
						,constraint [fk_artistID] foreign key ([ArtistID]) references [Artist] ([ArtistID])
					);

					insert into [MangaArtist](MangaID, ArtistID) select MangaID, ArtistID from [tmpMangaArtist];
					drop table [tmpMangaArtist];";
						ExecuteNonQuery(sQuery);

						sQuery = @"
					drop trigger trMangaTag;
					create temporary table [tmpMangaTag](MangaID, TagID);
					insert into [tmpMangaTag] select MangaID, TagID from [MangaTag];
					drop table [MangaTag];
        
					create table [MangaTag]
					(
						MangaTagID			integer		primary key		autoincrement
						,MangaID				integer		not null
						,TagID					integer		not null
						,constraint [fk_mangaID] foreign key ([MangaID]) references [Manga] ([MangaID])
						,constraint [fk_tagID] foreign key ([TagID]) references [Tag] ([TagID])
					);

					insert into [MangaTag](MangaID, TagID) select MangaID, TagID from [tmpMangaTag];
					drop table [tmpMangaTag];";
						ExecuteNonQuery(sQuery);
						#endregion

						EndTransaction();
						#endregion
						break;
					case 1:
						break;
				}
			}

			internal void Close()
			{
				if (sqConn != null
						&& sqConn.State != ConnectionState.Closed)
				{
					ExecuteNonQuery("VACUUM;");
					sqConn.Close();
				}
			}

			#endregion

			#region Create Database

			internal bool Import(string _filePath)
			{
				//load the old DB
				List<Main.csEntry> lData = FileSerializer.Deserialize
					<List<Main.csEntry>>(_filePath) ?? new List<Main.csEntry>(0);

				//input into new DB
				BeginTransaction();
				for (int i = 0; i < lData.Count; i++)
				{
					//ensure sizes are valid
					#region Force value sizes to be inside valid range
					if (lData[i].sArtist.Length > SQLITE_MAX_LENGTH)
						lData[i].sArtist = lData[i].sArtist.Substring(0, SQLITE_MAX_LENGTH);
					if (lData[i].sTitle.Length > SQLITE_MAX_LENGTH)
						lData[i].sTitle = lData[i].sTitle.Substring(0, SQLITE_MAX_LENGTH);
					if (lData[i].sDesc.Length > SQLITE_MAX_LENGTH)
						lData[i].sDesc = lData[i].sDesc.Substring(0, SQLITE_MAX_LENGTH);
					#endregion

					//add the entry
					SQLAccess.DB_SaveEntry(
						lData[i].sArtist, lData[i].sTitle, lData[i].dtDate,
						lData[i].sTags, lData[i].sLoc, lData[i].pages,
						lData[i].sType, lData[i].byRat, lData[i].sDesc, bConverting: true
					);

					if (delProgress != null)
					{
						delProgress.Invoke(i + 1);
					}
				}
				EndTransaction();
				lData.Clear();

				//deprecate old serialized DB
				try
				{
					File.Move(_filePath, _filePath + "_Deprecated");
				}
				catch (IOException)
				{
					Console.WriteLine("Could not alter old database");
				}

				return true;
			}

			internal void CreateDatabase()
			{
				BeginTransaction();
				Create_Artist();
				Create_Tag();
				Create_Type();
				Create_MangaArtist();
				Create_MangaTag();
				Create_Manga();
				Create_Settings();
				EndTransaction();
			}

			/// <summary>
			/// Create dbo.Artist
			/// </summary>
			internal void Create_Artist()
			{
				string sQuery = @"
				create table [Artist]
				(
					ArtistID				integer			primary key		autoincrement
					,Name						text				not null			unique
					,Psuedonym			text				null
					,CreatedDBTime	text				not null			default CURRENT_TIMESTAMP
					,AuditDBTime		text				not null			default CURRENT_TIMESTAMP
				);
        create trigger trArtist after update on Artist
				begin
					update Artist set AuditDBTime = CURRENT_TIMESTAMP where artistID = new.rowid;
				end;
			";
				ExecuteNonQuery(sQuery);
			}

			/// <summary>
			/// Create dbo.Tag
			/// </summary>
			internal void Create_Tag()
			{
				string sQuery = @"
				create table [Tag]
				(
					TagID						integer			primary key		autoincrement
					,Tag						text				not null			unique
					,CreatedDBTime	text				not null			default CURRENT_TIMESTAMP
					,AuditDBTime		text				not null			default CURRENT_TIMESTAMP
				);
        create trigger trTag after update on Tag
				begin
					update Tag set AuditDBTime = CURRENT_TIMESTAMP where tagID = new.rowid;
				end;
			";
				ExecuteNonQuery(sQuery);
			}

			/// <summary>
			/// Create dbo.Type and insert the default values
			/// </summary>
			internal void Create_Type()
			{
				string sQuery = @"
				create table [Type]
				(
					TypeID					integer		primary key		autoincrement
					,Type						text			not null			unique
					,CreatedDBTime	text			not null			default CURRENT_TIMESTAMP
					,AuditDBTime		text			not null			default CURRENT_TIMESTAMP
				);
        create trigger trType after update on Type
				begin
					update Type set AuditDBTime = CURRENT_TIMESTAMP where typeID = new.rowid;
				end;
        insert into [Type] (Type)
				values('Doujinshi'),('Manga'),('Artist CG'),('Game CG'),('Western')
          ,('Non-H'),('Image Set'),('Cosplay'),('Asian Porn'),('Misc');
			";
				ExecuteNonQuery(sQuery);
			}

			/// <summary>
			/// Create the linking table between Mangas and Artists
			/// </summary>
			internal void Create_MangaArtist()
			{
				string sQuery = @"
				create table [MangaArtist]
				(
					MangaArtistID		integer		primary key		autoincrement
					,MangaID				integer		not null
					,ArtistID				integer		not null
					,constraint [fk_mangaID] foreign key ([MangaID]) references [Manga] ([MangaID])
					,constraint [fk_artistID] foreign key ([ArtistID]) references [Artist] ([ArtistID])
				)
			";
				ExecuteNonQuery(sQuery);
			}

			/// <summary>
			/// Create the linking table between Mangas and Tags
			/// </summary>
			internal void Create_MangaTag()
			{
				string sQuery = @"
				create table [MangaTag]
				(
					MangaTagID			integer		primary key		autoincrement
					,MangaID				integer		not null
					,TagID					integer		not null
					,constraint [fk_mangaID] foreign key ([MangaID]) references [Manga] ([MangaID])
					,constraint [fk_tagID] foreign key ([TagID]) references [Tag] ([TagID])
				)
			";
				ExecuteNonQuery(sQuery);
			}

			//Create dbo.Manga
			internal void Create_Manga()
			{
				string sQuery = @"
				create table [Manga]
				(
					MangaID					integer			primary key		autoincrement
					,TypeID					integer			null
					,Title					text
					,Pages					integer			not null			default		0
					,Rating					numeric			not null			default		0
					,Description		text				null
					,Location				text				null
					,GalleryURL			text				null
					,PublishedDate	text				null
          ,Thumbnail      blob        null
					,CreatedDBTime	text				not null			default CURRENT_TIMESTAMP
					,AuditDBTime		text				not null			default CURRENT_TIMESTAMP
					,constraint [fk_typeID] foreign key ([TypeID]) references [Type] ([TypeID])
				);
        create trigger trManga after update on Manga
				begin
					update Manga set AuditDBTime = CURRENT_TIMESTAMP where mangaID = new.rowid;
				end;
       ";
				ExecuteNonQuery(sQuery);
			}

			/// <summary>
			/// Create dbo.Settings and insert the default values
			/// </summary>
			internal void Create_Settings()
			{
				string sQuery = @"
				create table [Settings]
				(
					SettingsID						integer			primary key		autoincrement
          ,DBversion            integer     not null      default   1
					,RootPath							text				null
					,SavePath							text				null
					,SearchIgnore					text				null
					,FormPosition					text				null
					,ImageBrowser					text				null
          ,Notes                text        null
					,member_id						integer			null
					,pass_hash						text				null
					,NewUser							integer			not null			default		1
					,SendReports					integer			not null			default		1
					,ShowGrid							integer			not null			default		1
					,ShowDate							integer			not null			default		1
					,ReadInterval					integer			not null			default		20000
					,RowColourHighlight		integer			not null			default		-15
					,RowColourAlt					integer			not null			default		-657931
					,BackgroundColour			integer			not null			default		-14211038
					,GallerySettings			text				not null			default		'1,1,0,0,0,0,0,0,0,0'
					,CreatedDBTime				text				not null			default		CURRENT_TIMESTAMP
					,AuditDBTime					text				not null			default		CURRENT_TIMESTAMP
				);
        create trigger trSettings after update on Settings
				begin
					update Settings set AuditDBTime = CURRENT_TIMESTAMP where settingsID = new.rowid;
				end;
        insert into [Settings] (DBVersion)
				values(1);
      ";
				ExecuteNonQuery(sQuery);
			}

			#endregion
			
			#region Convenience
			internal static string Cleanse(string sRaw)
			{
				return sRaw.Replace("'", "''").Replace(";", "");
			}

			internal static SQLiteParameter NewParameter(string ParameterName, DbType dbType, object value)
			{
				return new SQLiteParameter(ParameterName, dbType) {
					Value = value
				};
			}

			internal int BeginTransaction()
			{
				int iRetVal = 0;
				using (SQLiteCommand sqCmd = sqConn.CreateCommand())
				{
					sqCmd.CommandText = "begin transaction";
					iRetVal = sqCmd.ExecuteNonQuery();
				}
				return iRetVal;
			}

			internal int EndTransaction(int error = 0)
			{
				int iRetVal = 0;
				using (SQLiteCommand sqCmd = sqConn.CreateCommand())
				{
					sqCmd.CommandText = (error > -1 ? "commit" : "rollback") + " transaction";
					iRetVal = sqCmd.ExecuteNonQuery();
				}
				return iRetVal;
			}

			internal int ExecuteNonQuery(string CommandText,
				CommandBehavior cmd = CommandBehavior.Default, params SQLiteParameter[] sqParam)
			{
				int altered = 0;

				using (SQLiteCommand sqCmd = sqConn.CreateCommand())
				{
					sqCmd.Parameters.AddRange(sqParam);
					sqCmd.CommandText = CommandText;
					altered = sqCmd.ExecuteNonQuery(cmd);
				}

				return altered;
			}

			internal DataTable ExecuteQuery(string CommandText,
				CommandBehavior cmd = CommandBehavior.Default, params SQLiteParameter[] sqParam)
			{
				DataTable dt = new DataTable();

				using (SQLiteCommand sqCmd = sqConn.CreateCommand())
				{
					sqCmd.Parameters.AddRange(sqParam);
					sqCmd.CommandText = CommandText;

					using (SQLiteDataReader dr = sqCmd.ExecuteReader(cmd))
					{
						dt.Load(dr);
					}
				}

				return dt;
			}
			#endregion
		}

		private static class SQLAccess
		{
			#region Search Database

			internal static DataTable DB_Search(string sTerms, bool bOnlyFav = false, int iMangaID = -1)
			{
				if (string.IsNullOrWhiteSpace(sTerms))
				{
					return GetEntries();
				}

				//Set up variables
				StringBuilder sbCmd = new StringBuilder(5000);
				string[] asItems = Ext.Split(sTerms, " ");
				string[] asType = new string[asItems.Length];
				bool[][] abNot = new bool[asItems.Length][];
				string[][] asTerms = new string[asItems.Length][];

				#region Parse Terms
				for (int i = 0; i < asItems.Length; i++)
				{
					//check for type limiter
					string[] sSplit = asItems[i].Trim().Split(':');
					if (sSplit.Length > 1)
					{
						asType[i] = sSplit[0];
					}

					string[] asSubSplit = Ext.Split(sSplit[sSplit.Length > 1 ? 1 : 0], "&", ",");
					asTerms[i] = new string[asSubSplit.Length];
					for (int x = 0; x < asSubSplit.Length; x++)
					{
						asTerms[i][x] = asSubSplit[x];
					}

					//check for chained terms
					abNot[i] = new bool[asTerms[i].Length];
					for (int x = 0; x < asTerms[i].Length; x++)
					{
						asTerms[i][x] = asTerms[i][x].Replace('_', ' ');
						abNot[i][x] = asTerms[i][x].StartsWith("-");
						if (abNot[i][x])
							asTerms[i][x] = asTerms[i][x].Substring(1);
					}
				}
				#endregion

				#region Convert to SQL

				#region Data setup
				sbCmd.Append(vsManga);
				#endregion

				#region Where-clause setup
				sbCmd.AppendFormat(" where ({0} in (mgx.MangaID, -1)) "
					, iMangaID);

				if (bOnlyFav)
				{
					sbCmd.Append("and mgx.Rating = 5 ");
				}

				for (int i = 0; i < asTerms.Length; i++)
				{
					for (int x = 0; x < asTerms[i].Length; x++)
					{
						switch (asType[i])
						{
							case "artist":
							case "a":
								sbCmd.AppendFormat("and at.Name {0} like '%{1}%' "
									, abNot[i][x] ? "not" : ""
									, SQLBase.Cleanse(asTerms[i][x]));
								break;
							case "title":
							case "t":
								sbCmd.AppendFormat("and mgx.Title {0} like '%{1}%' "
									, abNot[i][x] ? "not" : ""
									, SQLBase.Cleanse(asTerms[i][x]));
								break;
							case "tag":
							case "tags":
							case "g":
								sbCmd.AppendFormat("and tg.Tags {0} like '%{1}%' "
									, abNot[i][x] ? "not" : ""
									, SQLBase.Cleanse(asTerms[i][x]));
								break;
							case "description":
							case "desc":
							case "s":
								sbCmd.AppendFormat("and mgx.Description {0} like '%{1}%' "
									, abNot[i][x] ? "not" : ""
									, SQLBase.Cleanse(asTerms[i][x]));
								break;
							case "type":
							case "y":
								sbCmd.AppendFormat("and tp.Type {0} like '%{1}%' "
									, abNot[i][x] ? "not" : ""
									, SQLBase.Cleanse(asTerms[i][x]));
								break;
							case "date":
							case "d":

								DateTime date = new DateTime();
								char c = !string.IsNullOrEmpty(asTerms[i][x]) ? asTerms[i][x][0] : ' ';

								if (DateTime.TryParse(asTerms[i][x].Substring(c != '<' && c != '>' ? 0 : 1), out date))
									sbCmd.AppendFormat("and date(mgx.PublishedDate) {0} date('{1}') "
										, abNot[i][x] ? '!' : (c == '<' || c == '>') ? c : '='
										, date.ToString("yyyy-MM-dd"));
								break;
							case "rating":
							case "r":
								c = !string.IsNullOrEmpty(asTerms[i][x]) ? asTerms[i][x][0] : ' ';
								int rat;

								if (int.TryParse(asTerms[i][x].Substring(c != '<' && c != '>' ? 0 : 1), out rat))
									sbCmd.AppendFormat("and mgx.Rating {0} {1} "
										, abNot[i][x] ? '!' : (c == '<' || c == '>') ? c : '='
										, rat);
								break;
							case "pages":
							case "page":
							case "p":
								c = !string.IsNullOrEmpty(asTerms[i][x]) ? asTerms[i][x][0] : ' ';
								int pg;

								if (int.TryParse(asTerms[i][x].Substring(c != '<' && c != '>' ? 0 : 1), out pg))
									sbCmd.AppendFormat("and mgx.Pages {0} {1} "
										, abNot[i][x] ? '!' : (c == '<' || c == '>') ? c : '='
										, pg);
								break;
							default:
								if (abNot[i][x])
								{
									sbCmd.AppendFormat("and (tg.Tags not like '%{0}%' and mgx.Title not like '%{0}%' and at.Name not like '%{0}%' and mgx.Description not like '%{0}%' and tp.Type not like '%{0}%' and date(mgx.PublishedDate) not like '%{0}%') "
									, SQLBase.Cleanse(asTerms[i][x]));
								}
								else
								{
									sbCmd.AppendFormat("and (tg.Tags like '%{0}%' or mgx.Title like '%{0}%' or at.Name like '%{0}%' or mgx.Description like '%{0}%' or tp.Type like '%{0}%' or date(mgx.PublishedDate) like '%{0}%') "
									, SQLBase.Cleanse(asTerms[i][x]));
								}

								break;
						}
					}
				}

				//append final syntax
				sbCmd.Append(vsMangaEnd);

				#endregion

				#endregion

				return sqlBase.ExecuteQuery(sbCmd.ToString(), CommandBehavior.Default);
			}
			#endregion

			#region Query Database

			/// <summary>
			/// Returns all the Artists in the database
			/// </summary>
			/// <returns></returns>
			internal static DataTable GetArtists()
			{
				string sCommandText = @"
					select
							at.ArtistID
						,ifnull(at.Name, '')				Artist
					from
						[Artist] at
					order by at.Name asc
				";

				return sqlBase.ExecuteQuery(sCommandText);
			}

			/// <summary>
			/// Returns all the Types in the database
			/// </summary>
			/// <returns></returns>
			internal static DataTable GetTypes()
			{
				string sCommandText = @"
					select
							tp.TypeID
						,tp.Type
					from
						[Type] tp
					order by tp.Type asc
				";

				return sqlBase.ExecuteQuery(sCommandText);
			}

			internal static DataTable GetTags()
			{
				string sCommandText = @"
					select
							tg.TagID
						,tg.Tag
					from
						[Tag] tg
					order by tg.Tag asc
				";

				return sqlBase.ExecuteQuery(sCommandText);
			}

			internal static DataTable GetEntries(bool bOnlyFav = false)
			{
				string sCommandText =
					vsManga
					+ (bOnlyFav ? "where mgx.Rating = 5" : "")
					+ vsMangaEnd;

				return sqlBase.ExecuteQuery(sCommandText);
			}

			internal static DataTable DB_GetEntryDetails(int mangaID)
			{
				string sCommandText = vsManga
					+ " where mgx.MangaID = @mangaID"
					+ vsMangaEnd;

				return sqlBase.ExecuteQuery(sCommandText, CommandBehavior.SingleRow
					, new SQLiteParameter("@mangaID", DbType.Int32) {
						Value = mangaID
					}
				);
			}

			internal static bool EntryExists(string sArtist, string sTitle)
			{
				bool bExists = false;
				string sCommandText = vsManga + @"
				where
					at.Name = @artist
				and
					mgx.Title = @title"
					+ vsMangaEnd;

				using (DataTable dt = sqlBase.ExecuteQuery(sCommandText, CommandBehavior.SingleRow
						, new SQLiteParameter("@artist", DbType.String) {
							Value = sArtist
						}
						, new SQLiteParameter("@title", DbType.String) {
							Value = sTitle
						}))
				{
					bExists = dt.Rows.Count > 0;
				}

				return bExists;
			}

			internal static DataTable DB_GetSettings()
			{
				string sCommandText = @"
				select 
						sx.SettingsID
            ,sx.DBVersion
						,sx.RootPath
						,sx.SavePath
						,sx.SearchIgnore
						,sx.FormPosition
						,sx.ImageBrowser
            ,sx.Notes
						,sx.member_id
						,sx.pass_hash
						,sx.NewUser
						,sx.SendReports
						,sx.ShowGrid
						,sx.ShowDate
						,sx.ReadInterval
						,sx.RowColourHighlight
						,sx.RowColourAlt
						,sx.BackgroundColour
						,sx.GallerySettings
						,sx.CreatedDBTime
						,sx.AuditDBTime
				from
					Settings sx
			";

				return sqlBase.ExecuteQuery(sCommandText);
			}

			#endregion

			#region Update Database

			internal static int DB_UpdateSetting(Setting DBSetting, object value)
			{
				//setup parameters
				SQLiteParameter sqParam = null;

				switch (DBSetting)
				{
					case Setting.BackgroundColour:
					case Setting.DBversion:
					case Setting.member_id:
					case Setting.NewUser:
					case Setting.ReadInterval:
					case Setting.RowColourAlt:
					case Setting.RowColourHighlight:
					case Setting.SendReports:
					case Setting.ShowDate:
					case Setting.ShowGrid:
						sqParam = new SQLiteParameter("@value", DbType.Int32) {
							Value = value
						};
						break;
					case Setting.FormPosition:
					case Setting.GallerySettings:
					case Setting.ImageBrowser:
					case Setting.Notes:
					case Setting.pass_hash:
					case Setting.RootPath:
					case Setting.SavePath:
					case Setting.SearchIgnore:
						sqParam = new SQLiteParameter("@value", DbType.String) {
							Value = value
						};
						break;
				}

				//determine whether to insert or update
				string sCommandText = string.Format(
					"update [Settings] set {0} = @value"
					, DBSetting.ToString());

				//run the command
				return sqlBase.ExecuteNonQuery(sCommandText, CommandBehavior.Default, sqParam);
			}

			internal static int DB_SaveEntry(string sArtist, string sTitle, DateTime dtPubDate,
					string sTags = null, string sLoc = null, decimal iPages = 0, string sType = null,
					decimal dRating = 0, string sDesc = null, string sURL = null, int iMangaID = -1,
					bool bConverting = false)
			{
				if (!bConverting)
					sqlBase.BeginTransaction();

				//setup parameters
				StringBuilder sbCmd = new StringBuilder(10000);
				List<SQLiteParameter> lParam = new List<SQLiteParameter>(50);
				lParam.AddRange(new SQLiteParameter[11] {
        SQLBase.NewParameter("@mangaID", DbType.Int32, iMangaID)
        , SQLBase.NewParameter("@title", DbType.String, sTitle)
        , SQLBase.NewParameter("@name", DbType.String, sArtist)
        , SQLBase.NewParameter("@pages", DbType.Int32, Convert.ToInt32(iPages))
        , SQLBase.NewParameter("@rating", DbType.Decimal, dRating)
        , SQLBase.NewParameter("@description", DbType.String, sDesc)
        , SQLBase.NewParameter("@location", DbType.String, sLoc)
        , SQLBase.NewParameter("@URL", DbType.String, sURL)
        , SQLBase.NewParameter("@pubDate", DbType.String, dtPubDate.ToString("yyyy-MM-dd"))
        , SQLBase.NewParameter("@name", DbType.String, sArtist)
        , SQLBase.NewParameter("@type", DbType.String, sType)
      });

				#region Update the base Manga record
				//determine whether to insert or update
				string sCommandText;
				if (iMangaID == -1)
				{
					sCommandText = @"
					insert into [Manga](title, pages, rating, description, location, galleryURL, publishedDate)
					values(
            @title
					  ,@pages
					  ,@rating
					  ,case when @description <> '' then @description else null end
					  ,case when @location <> '' then @location else null end
					  ,case when @URL <> '' then @URL else null end
					  ,@pubDate
      )";
				}
				else
				{
					sCommandText = @"
					update [Manga]
					set title = @title
					,pages = @pages
					,rating = @rating
					,description = case when @description <> '' then @description else null end
					,location = case when @location <> '' then @location else null end
					,galleryURL = case when @URL <> '' then @URL else null end
					,publishedDate = @pubDate
					where MangaID = @mangaID";
				}
				sqlBase.ExecuteNonQuery(sCommandText, CommandBehavior.Default, lParam.ToArray());
				#endregion

				//set the mangaID parameter if necessary
				if (iMangaID == -1)
				{
					using (DataTable dt = sqlBase.ExecuteQuery("select max(MangaID) from Manga", CommandBehavior.SingleRow))
					{
						iMangaID = Int32.Parse(dt.Rows[0][0].ToString());
						lParam[0].Value = iMangaID;
					}
				}

				#region update the artist and manga type

				sbCmd.Append(@"
				--insert/update the manga artist
				insert into [Artist](Name)
				select @name
				where 
					@name <> ''
				and
					not exists(select 1 from [Artist] where Name = @name);

        insert into [MangaArtist](MangaID, ArtistID)
				select @mangaID, ArtistID
        from [Artist] art
				where 
          Name = @name 
        and 
          not exists(select 1 from [MangaArtist] mat where MangaID = @mangaID and mat.ArtistID = art.ArtistID);

        delete from [MangaArtist] 
				where MangaID = @mangaID and ArtistID not in (
          select ArtistID from [Artist] where Name = @name);

				--insert/update the manga type
				insert into [Type](Type)
				select @type
				where 
					@type <> ''
				and
					not exists(select 1 from [Type] where Type = @type);

        update [Manga]
        set TypeID = case when @type = '' then null else (select TypeID from [Type] where Type = @type) end 
        where 
            MangaID = @mangaID 
        and 
          (TypeID is null or TypeID not in (select TypeID from [Type] where Type = @type));"
				);

				#endregion

				#region Update the tags

				string[] asTags = SQLBase.Cleanse(sTags).Split(',').Select(x => x.Trim()).ToArray();
				sTags = string.Format("'{0}'", String.Join("','", asTags));
				lParam.Add(SQLBase.NewParameter("@mangaID", DbType.Int32, iMangaID));

				for (int i = 0; i < asTags.Length; i++)
				{
					if (string.IsNullOrWhiteSpace(asTags[i]))
						continue;
					lParam.Add(SQLBase.NewParameter("@tag" + i.ToString(), DbType.String, asTags[i]));

					sbCmd.AppendFormat("insert into [Tag](Tag) select {0} where {0} <> '' and not exists(select 1 from [Tag] where Tag = {0}); insert into [MangaTag](MangaID, TagID) select @mangaID, TagID from [Tag] tg where Tag = {0} and not exists(select 1 from [MangaTag] mtg where MangaID = @mangaID and mtg.TagID = tg.TagID);",
						"@tag" + i.ToString());
				}
				sbCmd.AppendFormat("delete from [MangaTag] where MangaID = @mangaID and TagID not in (select TagID from [Tag] where Tag in ({0}))", sTags);

				#endregion

				sqlBase.ExecuteNonQuery(sbCmd.ToString(), CommandBehavior.Default, lParam.ToArray());

				if (!bConverting)
					sqlBase.EndTransaction();

				return iMangaID;
			}

			internal static int Entry_Delete(int iMangaID)
			{
				sqlBase.BeginTransaction();
				SQLiteParameter sqParam = SQLBase.NewParameter("@mangaID", DbType.Int32, iMangaID);

				string sCommandText = @"
				delete from MangaArtist
				where MangaID = @mangaID;
        delete from MangaTag
				where MangaID = @mangaID;
        delete from Manga
				where MangaID = @mangaID;";
				int altered = sqlBase.ExecuteNonQuery(sCommandText, CommandBehavior.Default, sqParam);

				sqlBase.EndTransaction();
				return altered;
			}

			internal static int DeleteUnusedTags()
			{
				sqlBase.BeginTransaction();

				string sCommandText = @"
				delete from Tag
				where TagID not in 
				(select TagID from MangaTag)";
				int altered = sqlBase.ExecuteNonQuery(sCommandText);

				sqlBase.EndTransaction();
				return altered;
			}

			#endregion
		}
  }
}
