﻿using FileBotPP.Helpers;
using FileBotPP.Metadata.Interfaces;
using FileBotPP.Tree.Interfaces;

namespace FileBotPP.Tree
{
    public class FileItem : Item, IFileItem
    {
        public FileItem() : base( false )
        {
            this.Parent?.Update();
            this.ItemSuggestedName = "";
        }

        public override string ShortName { get; set; }
        public override string Extension { get; set; }
        public override string NewPath { get; set; }
        public override bool NewPathExists { get; set; }
        public override bool Duplicate { get; set; }
        public override string TorrentLink { get; set; }

        public override bool AllowedType
        {
            get { return this.Extension == null || AllowedTypes.Contains( this.Extension.ToLower() ); }
            set
            {
                this.ItemAllowedType = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "AllowedType" );
            }
        }

        public override bool Missing
        {
            get { return this.ItemMissing; }
            set
            {
                this.ItemMissing = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "Missing" );
            }
        }

        public override bool Extra
        {
            get { return this.ItemExtra; }
            set
            {
                this.ItemExtra = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "Extra" );
            }
        }

        public override bool BadLocation
        {
            get { return this.ItemBadLocation; }
            set
            {
                this.ItemBadLocation = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "BadLocation" );
            }
        }

        public override bool BadName
        {
            get { return this.ItemBadName; }
            set
            {
                this.ItemBadName = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "BadName" );
            }
        }

        public override bool BadQuality
        {
            get
            {
                if ( this.Mediainfo != null )
                {
                    try
                    {
                        if ( int.Parse( this.Mediainfo.VideoHeight ) < Settings.PoorQualityP )
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
                return this.ItemBadQuality;
            }
            set
            {
                this.ItemBadQuality = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "BadQuality" );
            }
        }

        public override bool Corrupt
        {
            get { return this.ItemCorrupt; }
            set
            {
                this.ItemCorrupt = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "Corrupt" );
            }
        }

        public override string SuggestedName
        {
            get { return this.ItemSuggestedName; }
            set
            {
                this.ItemSuggestedName = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "SuggestedName" );
            }
        }

        public override IMediaInfo Mediainfo
        {
            get { return this.ItemMediaInfo; }
            set
            {
                this.ItemMediaInfo = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "Mediainfo" );
            }
        }

        public override bool Torrent
        {
            get { return this.ItemTorrent; }
            set
            {
                this.ItemTorrent = value;
                this.Parent?.Update();
                this.OnPropertyChanged( "Torrent" );
            }
        }

        public override int Count => this.Missing ? 0 : 1;
    }
}