﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using FileBotPP.Helpers;
using FileBotPP.Tree.Interfaces;

namespace FileBotPP.Tree
{
    public class FsPoller : IFsPoller
    {
        private static readonly List< FsPoller > Fspollers;
        private static readonly Object Lockobj = new Object();
        private static readonly Random Random;
        private readonly IDirectoryItem _directory;
        private readonly DirectoryInfo _directoryInfo;
        private DispatcherTimer _timer;

        static FsPoller()
        {
            Fspollers = new List< FsPoller >();
            Random = new Random();
        }

        public FsPoller()
        {
            this._directory = null;
            this._directoryInfo = new DirectoryInfo( Common.ScanLocation );
            Fspollers.Add( this );
        }

        public FsPoller( IDirectoryItem directory )
        {
            this._directory = directory;
            this._directoryInfo = new DirectoryInfo( this._directory.Path );
            Fspollers.Add( this );
        }

        public void start_poller()
        {
            this._timer = new DispatcherTimer();
            this._timer.Interval = new TimeSpan( 0, 0, Random.Next( Settings.FilesSystemWatcherMinRefreshTime, Settings.FilesSystemWatcherMaxRefreshTime ) );
            this._timer.Tick += this.TimerOnElapsed;
            this._timer.IsEnabled = true;
            this._timer.Start();
        }

        public void stop_poller()
        {
            try
            {
                this._timer.Tick -= this.TimerOnElapsed;
                this._timer.IsEnabled = false;
                this._timer.Stop();
            }
            catch ( Exception ex )
            {
                Utils.LogLines.Enqueue( ex.Message );
                Utils.LogLines.Enqueue( ex.StackTrace );
            }
        }

        private void TimerOnElapsed( object sender, EventArgs elapsedEventArgs )
        {
            try
            {
                this._timer.Interval = new TimeSpan( 0, 0, Random.Next( Settings.FilesSystemWatcherMinRefreshTime, Settings.FilesSystemWatcherMaxRefreshTime ) );

                lock (Lockobj)
                {
                    this.add_items();
                    this.remove_items();
                }
            }
            catch ( Exception ex )
            {
                Utils.LogLines.Enqueue( ex.Message );
                Utils.LogLines.Enqueue( ex.StackTrace );
            }
        }

        private void add_items()
        {
            try
            {
                foreach ( var dir in this._directoryInfo.GetDirectories() )
                {
                    var exists = this._directory == null ? ItemProvider.ContainsDirectory( dir.Name ) : this._directory.ContainsDirectory( dir.Name );

                    if ( exists != null )
                    {
                        continue;
                    }

                    var item = new DirectoryItem {FullName = dir.Name, Path = dir.FullName, Parent = this._directory, Polling = true};
                    ItemProvider.insert_item_ordered_threadsafe( item );
                    ItemProvider.refresh_tree_directory( item, item.Path );
                    ItemProvider.folder_scan_update_threadsafe();
                }

                foreach ( var file in this._directoryInfo.GetFiles() )
                {
                    var exists = this._directory == null ? ItemProvider.ContainsFile( file.Name ) : this._directory.ContainsFile( file.Name );

                    if ( exists != null )
                    {
                        continue;
                    }

                    var shortname = file.Name;
                    var extension = "";

                    if ( file.Name.Contains( "." ) )
                    {
                        shortname = file.Name.Substring( 0, file.Name.LastIndexOf( ".", StringComparison.Ordinal ) );
                        extension = file.Name.Substring( file.Name.LastIndexOf( ".", StringComparison.Ordinal ) + 1 );
                    }

                    var item = new FileItem
                    {
                        FullName = file.Name,
                        ShortName = shortname,
                        Extension = extension,
                        Path = file.FullName,
                        Parent = this._directory
                    };

                    ItemProvider.insert_item_ordered_threadsafe( item );
                    ItemProvider.folder_scan_update_threadsafe();
                }
            }
            catch ( Exception ex )
            {
                Utils.LogLines.Enqueue( ex.Message );
                Utils.LogLines.Enqueue( ex.StackTrace );
            }
        }

        private bool fs_contains_directory( string name )
        {
            foreach ( var dirinfo in this._directoryInfo.GetDirectories() )
            {
                if ( String.Compare( dirinfo.Name, name, StringComparison.Ordinal ) == 0 )
                {
                    return true;
                }
            }
            return false;
        }

        private bool fs_contains_file( string name )
        {
            foreach ( var dirinfo in this._directoryInfo.GetFiles() )
            {
                if ( String.Compare( dirinfo.Name, name, StringComparison.Ordinal ) == 0 )
                {
                    return true;
                }
            }
            return false;
        }

        private void remove_items()
        {
            try
            {
                var removedirs = new List< IDirectoryItem >();
                var removefiles = new List< IFileItem >();

                var diritems = this._directory == null ? ItemProvider.Items : this._directory.Items;

                foreach ( var dir in diritems.OfType< IDirectoryItem >().Where( directory => directory.Missing == false ) )
                {
                    var exists = this.fs_contains_directory( dir.FullName );

                    if ( exists == false )
                    {
                        removedirs.Add( dir );
                    }
                }

                var fileitems = this._directory == null ? ItemProvider.Items : this._directory.Items;

                foreach ( var file in fileitems.OfType< IFileItem >().Where( file => file.Missing == false ) )
                {
                    var exists = this.fs_contains_file( file.FullName );

                    if ( exists == false )
                    {
                        removefiles.Add( file );
                    }
                }

                foreach ( var removedir in removedirs )
                {
                    ItemProvider.delete_folder_in_memory( removedir );
                }

                foreach ( var removefile in removefiles )
                {
                    ItemProvider.delete_file_in_memory( removefile );
                }
            }
            catch ( Exception ex )
            {
                Utils.LogLines.Enqueue( ex.Message );
                Utils.LogLines.Enqueue( ex.StackTrace );
            }
        }

        public static void stop_all()
        {
            try
            {
                foreach ( var fspoller in Fspollers )
                {
                    fspoller.stop_poller();
                }
            }
            catch ( Exception ex )
            {
                Utils.LogLines.Enqueue( ex.Message );
                Utils.LogLines.Enqueue( ex.StackTrace );
            }
        }

        public static void start_all()
        {
            try
            {
                if ( Settings.FilesSystemWatcherEnabled == false )
                {
                    return;
                }

                foreach ( var fspoller in Fspollers )
                {
                    fspoller.start_poller();
                }
            }
            catch ( Exception ex )
            {
                Utils.LogLines.Enqueue( ex.Message );
                Utils.LogLines.Enqueue( ex.StackTrace );
            }
        }
    }
}