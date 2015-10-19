﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FileBotPP.Interfaces;
using FileBotPP.Metadata;
using FileBotPP.Metadata.eztv;
using FileBotPP.Tree;

namespace FileBotPP.Helpers
{
    public class SeriesAnalyzer : ISeriesAnalyzer
    {
        private BackgroundWorker _scanAnalyzer;

        public void fetch_tvdb_metadata()
        {
            var dirs = Directory.GetDirectories( Common.ScanLocation );
            Common.Tvdb = new Tvdb( dirs.Select( dir => dir.Split( '\\' ) ).Select( nameparts => nameparts[ nameparts.Length - 1 ] ).ToArray() );
            Common.Tvdb.downloads_series_data();
        }

        public void fetch_eztv_metadata()
        {
            Common.Eztv = new Eztv();
            Common.Eztv.downloads_series_data();
        }

        public void analyze_series_folder()
        {
            Common.FileBotPp.set_status_text( "Analysing..." );

            this._scanAnalyzer = new BackgroundWorker {WorkerReportsProgress = true};
            this._scanAnalyzer.DoWork += this.ScanAnalyzer_DoWork;
            this._scanAnalyzer.RunWorkerCompleted += ScanAnalyzer_RunWorkerCompleted;
            this._scanAnalyzer.RunWorkerAsync();
        }

        private static void clean_series_season_duplicates( IDirectoryItem directory )
        {
            var checklist = new List<IFileItem>();

            foreach ( var fileitem in directory.Items.OfType<IFileItem>() )
            {
                foreach ( var checkitem in checklist )
                {
                    if ( checkitem.ShortName != fileitem.ShortName )
                    {
                        continue;
                    }

                    ItemProvider.DuplicateFiles.Enqueue( new DuplicateUpdate {Directory = directory, FileA = checkitem, FileB = fileitem} );
                }

                checklist.Add( fileitem );
            }
        }

        private static void clean_series_season_files( IDirectoryItem directory )
        {
            foreach ( var subdirectory in directory.Items.OfType< IDirectoryItem >() )
            {
                foreach ( var file in subdirectory.Items.OfType< IFileItem >() )
                {
                    if ( String.Compare( file.FullName, "Thumbs.db", StringComparison.Ordinal ) == 0 )
                    {
                        ItemProvider.DirectoryDeletions.Enqueue( new DeletionUpdate {Directory = subdirectory, File = file} );
                        continue;
                    }

                    var seasonNum = subdirectory.FullName.Substring( 7 ).Trim();

                    var test1 = @"^" + Regex.Escape( directory.FullName ) + @"\s+-\s+(" + seasonNum + @")x[0-9]{2}";
                    var test2 = @"^" + Regex.Escape( directory.FullName ) + @"\s+-\s+(" + seasonNum + @")xSpecial\s+[0-9]+";

                    if ( Regex.IsMatch( file.FullName, test1, RegexOptions.IgnoreCase ) || Regex.IsMatch( file.FullName, test2, RegexOptions.IgnoreCase ) )
                    {
                        clean_series_season_files_check_episode_name( directory, subdirectory, file );
                        clean_series_season_file_check_extra( directory, subdirectory, file );
                        continue;
                    }
                    if ( Regex.IsMatch( file.FullName, @"^" + Regex.Escape( directory.FullName ) + @"\.? - (\[(.*?)\]).*?(\[(.*)\])", RegexOptions.IgnoreCase ) )
                    {
                        continue;
                    }

                    if ( Regex.IsMatch( file.FullName, @"^" + Regex.Escape( directory.FullName ) + @".*" ) == false )
                    {
                        ItemProvider.BadNameFiles.Enqueue( new BadNameUpdate {Directory = directory, File = file, SuggestName = ""} );
                    }
                    else if ( Regex.IsMatch( file.FullName, @".*?(" + seasonNum + @")x([0-9]{2}|(" + seasonNum + @")xSpecial\s+?[0-9]+)" ) == false )
                    {
                        const string stest1 = @".*?([0-9]+)x([0-9]{2})";
                        const string stest2 = @"([0-9]+)xSpecial\s+?([0-9]+)";

                        var match1 = Regex.Match( file.FullName, stest1 );
                        var match2 = Regex.Match( file.FullName, stest2 );

                        if ( !match1.Success && !match2.Success )
                        {
                            ItemProvider.BadNameFiles.Enqueue( new BadNameUpdate {Directory = directory, File = file, SuggestName = ""} );
                            continue;
                        }

                        var seasonint = match1.Success ? match1.Groups[ 1 ].Value : match2.Groups[ 1 ].Value;

                        var newdirectory = directory.Path + "\\Season " + seasonint;
                        var newfilelocation = directory.Path + "\\Season " + seasonint + "\\" + file.FullName;

                        ItemProvider.BadLocationFiles.Enqueue( new BadLocationUpdate {Directory = subdirectory, File = file, NewPath = newfilelocation} );

                        if ( Directory.Exists( newdirectory ) )
                        {
                            continue;
                        }

                        var missingdirectory = new DirectoryItem {Parent = (IItem) directory, FullName = "Season " + seasonint, Path = newdirectory};
                        var di = new DirectoryInsert {Directory = directory, SubDirectory = missingdirectory, Seasonnum = int.Parse( seasonint )};
                        ItemProvider.NewDirectoryUpdates.Enqueue( di );
                    }
                }

                clean_series_season_files_find_missing( directory, subdirectory );
                clean_series_season_duplicates( directory );
            }
        }

        private static void clean_series_season_files_check_episode_name( IDirectoryItem directory, IItem subdirectory, IFileItem item )
        {
            if ( Common.Tvdb == null )
            {
                return;
            }

            var cSeasonNum = int.Parse( subdirectory.FullName.Substring( 7 ).Trim() );

            var series = Common.Tvdb.get_series_by_name( directory.FullName );

            if ( series == null )
            {
                return;
            }

            if ( series.has_seasons( cSeasonNum ) == false )
            {
                return;
            }

            var season = series.get_season( cSeasonNum );
            var episodes = season.get_episodes();

            var clean = false;

            foreach ( var episode in episodes )
            {
                var epnameclean = episode.get_episode_name().ToLower();
                epnameclean = epnameclean.Replace( ":", "" );
                epnameclean = epnameclean.Replace( "?", "" );
                epnameclean = Regex.Replace( epnameclean, ".+", "." );

                if ( item.FullName.ToLower().Contains( epnameclean ) )
                {
                    clean = true;
                    break;
                }
            }

            if ( clean == false )
            {
                ItemProvider.BadNameFiles.Enqueue( new BadNameUpdate {Directory = directory, File = item, SuggestName = ""} );
            }
        }

        private static void clean_series_season_file_check_extra( IDirectoryItem directory, IItem subdirectory, IFileItem item )
        {
            if ( Common.Tvdb == null )
            {
                return;
            }

            var cSeasonNum = int.Parse( subdirectory.FullName.Substring( 7 ).Trim() );

            var series = Common.Tvdb.get_series_by_name( directory.FullName );

            if ( series == null )
            {
                return;
            }

            if ( series.has_seasons( cSeasonNum ) == false )
            {
                return;
            }

            const string stest1 = @".*?([0-9]+)x([0-9]{2})";
            const string stest2 = @"([0-9]+)xSpecial\s+?([0-9]+)";

            var match1 = Regex.Match( item.FullName, stest1 );
            var match2 = Regex.Match( item.FullName, stest2 );

            var epint = match1.Success ? match1.Groups[ 2 ].Value : match2.Groups[ 2 ].Value;

            if ( series.get_season( cSeasonNum ).get_episodes().Any( episode => episode.get_episode_num() == int.Parse( epint ) ) )
            {
                return;
            }

            ItemProvider.ExtraFiles.Enqueue( new ExtraFileUpdate {Directory = directory, File = item} );
        }

        private static void clean_series_season_files_find_missing( IDirectoryItem directory, IDirectoryItem subdirectory )
        {
            if ( Common.Tvdb == null )
            {
                return;
            }

            int cSeasonNum;

            try
            {
                cSeasonNum = int.Parse( subdirectory.FullName.Substring( 7 ).Trim() );
            }
            catch ( Exception )
            {
                Utils.LogLines.Enqueue( "Unable to determine season number" + subdirectory.Path );
                ItemProvider.BadNameFiles.Enqueue( new BadNameUpdate {Directory = directory, File = null, SuggestName = ""} );
                return;
            }

            var series = Common.Tvdb.get_series_by_name( directory.FullName );

            if ( series == null )
            {
                return;
            }

            if ( series.has_seasons( cSeasonNum ) == false )
            {
                return;
            }

            var episodes = series.get_season( cSeasonNum ).get_episodes();

            foreach ( var episode in episodes )
            {
                var epnum = cSeasonNum + "x" + string.Format( "{0:00}", episode.get_episode_num() );
                var found = subdirectory.Items.OfType< IFileItem >().Any( file => Regex.IsMatch( file.FullName, ".*?" + epnum + ".*?" ) );

                if ( found )
                {
                    continue;
                }

                var newfile = new FileItem {FullName = directory.FullName + " - " + epnum + " - " + episode.get_episode_name(), Parent = (IItem) subdirectory, Missing = true};

                var fi = new FileInsert {Directory = subdirectory, File = newfile, EpisodeNum = episode.get_episode_num()};

                var torrent = clean_series_season_files_find_torrent( series.ImdbId, episode.get_episode_num(), cSeasonNum );

                if ( torrent.Epname != null && String.CompareOrdinal( torrent.Epname, "" ) != 0 )
                {
                    newfile.Torrent = true;
                    newfile.TorrentLink = torrent.Magnetlink;
                }

                ItemProvider.NewFilesUpdates.Enqueue( fi );
            }
        }

        private static ITorrent clean_series_season_files_find_torrent( string imdbid, int epnum, int seasonnum )
        {
            ITorrent preferred = null;
            ITorrent t720 = null;
            ITorrent t1080 = null;
            ITorrent thdtv = null;

            foreach ( var torrent in Common.Eztv.get_torrents() )
            {
                if ( String.Compare( torrent.Imbdid, imdbid, StringComparison.Ordinal ) != 0 )
                {
                    continue;
                }

                var epname = torrent.Epname.ToLower();

                if ( epname.Contains( "s" + String.Format( "{0:00}", seasonnum ) + "e" + string.Format( "{0:00}", epnum ) ) )
                {
                    if ( epname.Contains( Settings.TorrentPreferredQuality ) )
                    {
                        preferred = torrent;
                        continue;
                    }
                    if ( epname.Contains( "720p" ) )
                    {
                        t720 = torrent;
                        continue;
                    }
                    if ( epname.Contains( "1080p" ) )
                    {
                        t1080 = torrent;
                        continue;
                    }

                    thdtv = torrent;
                }
            }

            if ( preferred?.Magnetlink?.Length > 0 )
            {
                return preferred;
            }

            if ( t1080?.Magnetlink?.Length > 0 )
            {
                return t1080;
            }

            if ( t720?.Magnetlink?.Length > 0 )
            {
                return t720;
            }

            if ( thdtv?.Magnetlink?.Length > 0 )
            {
                return thdtv;
            }

            return new Torrent();
        }

        private static void clean_series_season_directories( IItem directory )
        {
            foreach ( var seasondirectory in directory.Items.OfType< IDirectoryItem >() )
            {
                foreach ( var subdirectory in seasondirectory.Items.OfType< IDirectoryItem >() )
                {
                    ItemProvider.BadLocationFiles.Enqueue( new BadLocationUpdate {Directory = subdirectory, File = null, NewPath = ""} );

                    if ( subdirectory.Items.Count != 0 )
                    {
                        continue;
                    }

                    ItemProvider.DirectoryDeletions.Enqueue( new DeletionUpdate {Directory = subdirectory} );
                }
            }
        }

        private static void clean_series_top_level_files( IDirectoryItem directory )
        {
            foreach ( var item in directory.Items.OfType< IFileItem >() )
            {
                Utils.LogLines.Enqueue( @"Found files at top level " + directory.FullName );

                if ( String.Compare( item.FullName, "Thumbs.db", StringComparison.Ordinal ) == 0 )
                {
                    ItemProvider.DirectoryDeletions.Enqueue( new DeletionUpdate {Directory = directory, File = item} );
                    continue;
                }

                var match = Regex.Match( item.FullName, "^" + directory.FullName + "\\.? - (\\d+)x[0-9]+" );

                if ( match.Success )
                {
                    var seasonNum = int.Parse( match.Groups[ 1 ].ToString() );
                    var checkdir = directory.Path + "\\Season " + seasonNum;

                    item.BadLocation = true;

                    if ( File.Exists( checkdir ) == false )
                    {
                        Utils.LogLines.Enqueue( @"Created Directory: " + checkdir );
                    }

                    if ( File.Exists( checkdir + "\\" + item.FullName ) )
                    {
                        item.NewPathExists = true;
                    }

                    ItemProvider.BadLocationFiles.Enqueue( new BadLocationUpdate {Directory = directory, File = item, NewPath = checkdir + "\\" + item.FullName} );
                }
                else
                {
                    ItemProvider.BadNameFiles.Enqueue( new BadNameUpdate {Directory = directory, File = item, SuggestName = ""} );
                }
            }
        }

        private static void clean_series_top_level_directories( IDirectoryItem directory )
        {
            if ( directory?.Items == null )
            {
                return;
            }

            foreach ( var inode in directory.Items.OfType< IDirectoryItem >() )
            {
                if ( Regex.IsMatch( inode.FullName, @"^Season ([0-9]+)" ) )
                {
                    continue;
                }

                Utils.LogLines.Enqueue( @"Dirty directory name : " + inode.Path );
                ItemProvider.BadNameFiles.Enqueue( new BadNameUpdate {Directory = inode, File = null, SuggestName = ""} );
            }

            if ( Common.Tvdb == null )
            {
                return;
            }

            foreach ( var series in Common.Tvdb.get_series() )
            {
                if ( String.Compare( directory.FullName, series.get_name(), StringComparison.Ordinal ) != 0 )
                {
                    continue;
                }

                foreach ( var season in series.get_seasons() )
                {
                    var check = @"Season " + season.get_season_num();

                    if ( ItemProvider.contains_child( directory, check ) )
                    {
                        continue;
                    }

                    var missingseason = new DirectoryItem {FullName = check, Path = "", Missing = true};

                    foreach ( var episode in season.get_episodes() )
                    {
                        var epnum = season.get_season_num() + "x" + string.Format( "{0:00}", episode.get_episode_num() );
                        var fileitem = new FileItem {FullName = directory.FullName + " - " + epnum + " - " + episode.get_episode_name(), Missing = true};
                        missingseason.Items.Add( fileitem );
                    }

                    var di = new DirectoryInsert {Directory = directory, SubDirectory = missingseason, Seasonnum = season.get_season_num()};
                    ItemProvider.NewDirectoryUpdates.Enqueue( di );
                }

                break;
            }
        }

        private static void ScanAnalyzer_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            ItemProvider.update_model();
            Common.FileBotPp.set_status_text( "Analysis complete" );
        }

        private void ScanAnalyzer_DoWork( object sender, DoWorkEventArgs e )
        {
            foreach ( var inode in ItemProvider.Items.OfType< IFileItem >() )
            {
                Utils.LogLines.Enqueue( @"File found outside series directory: " + inode.Path );
            }

            foreach ( var inode in ItemProvider.Items.OfType< IDirectoryItem >() )
            {
                clean_series_top_level_directories( inode );
                clean_series_top_level_files( inode );
                clean_series_season_directories( inode );
                clean_series_season_files( inode );
            }
        }
    }
}