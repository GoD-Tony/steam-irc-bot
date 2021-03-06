﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SteamKit2;

namespace SteamIrcBot
{
    class PackageInfoCommand : Command<PackageInfoCommand.Request>
    {
        public class Request : BaseRequest
        {
            public JobID JobID { get; set; }

            public uint PackageID { get; set; }
        }


        public PackageInfoCommand()
        {
            Triggers.Add( "!packageinfo" );
            HelpText = "!packageinfo <packageid> - Requests package info for a given PackageID, and serves it";

            Steam.Instance.CallbackManager.Subscribe<SteamApps.PICSProductInfoCallback>( OnProductInfo );
        }

        protected override void OnRun( CommandDetails details )
        {
            if ( !Settings.Current.IsWebEnabled )
            {
                IRC.Instance.Send( details.Channel, "{0}: Web support is disabled", details.Sender.Nickname );
                return;
            }

            if ( details.Args.Length == 0 )
            {
                IRC.Instance.Send( details.Channel, "{0}: PackageID argument required", details.Sender.Nickname );
                return;
            }

            uint packageId;
            if ( !uint.TryParse( details.Args[ 0 ], out packageId ) )
            {
                IRC.Instance.Send( details.Channel, "{0}: Invalid PackageID", details.Sender.Nickname );
                return;
            }

            if ( !Steam.Instance.Connected )
            {
                IRC.Instance.Send( details.Channel, details.Sender.Nickname + ": Unable to request package info: not connected to Steam!" );
                return;
            }

            var jobId = Steam.Instance.Apps.PICSGetProductInfo( null, packageId, false, false );
            AddRequest( details, new Request { JobID = jobId, PackageID = packageId } );
        }

        void OnProductInfo( SteamApps.PICSProductInfoCallback callback )
        {
            if ( callback.ResponsePending )
                return;

            var req = GetRequest( r => r.JobID == callback.JobID );

            if ( req == null )
                return;

            bool isUnknownPackage = callback.UnknownPackages.Contains( req.PackageID ) || !callback.Packages.ContainsKey( req.PackageID );

            if ( isUnknownPackage )
            {
                IRC.Instance.Send( req.Channel, "{0}: Unable to request package info for {1}: unknown PackageID", req.Requester.Nickname, req.PackageID );
                return;
            }

            var packageInfo = callback.Packages[ req.PackageID ];
            var kv = packageInfo.KeyValues.Children
                .FirstOrDefault(); // sigh, inconsistencies

            var path = Path.Combine( "packageinfo", string.Format( "{0}.vdf", req.PackageID ) );
            var fsPath = Path.Combine( Settings.Current.WebPath, path );


            var webUri = new Uri( new Uri( Settings.Current.WebURL ), path );

            try
            {
                kv.SaveToFile( fsPath, false );
            }
            catch ( IOException ex )
            {
                IRC.Instance.Send( req.Channel, "{0}: Unable to save package info for {1} to web path!", req.Requester.Nickname, req.PackageID );
                Log.WriteError( "PackageInfoCommand", "Unable to save package info for {0} to web path: {1}", req.PackageID, ex );

                return;
            }

            IRC.Instance.Send( req.Channel, "{0}: {1} {2}", req.Requester.Nickname, webUri, packageInfo.MissingToken ? "(requires token)" : "" );
        }
    }
}
