﻿using System.Net;
using MessagePack;
using System.Collections.Generic;

namespace CryCrawler
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class Configuration
    {
        public HostConfiguration HostConfig { get; set; } = new HostConfiguration();
        public WorkerConfiguration WorkerConfig { get; set; } = new WorkerConfiguration();
        public WebGUIEndPoint WebGUI { get; set; } = new WebGUIEndPoint();
        public string CacheFilename { get; set; } = CacheDatabase.DefaultFilename;
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class HostConfiguration
    {
        public HostListeningEndPoint ListenerConfiguration { get; set; } = new HostListeningEndPoint();
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class WorkerConfiguration
    {
        public HostEndPoint HostEndpoint { get; set; } = new HostEndPoint();

        public string DownloadsPath { get; set; } = "downloads";
        public bool DontCreateSubfolders { get; set; } = false;
        public int MaxConcurrency { get; set; } = 3;
        public bool DepthSearch { get; set; } = false;
        public int MaxLoggedDownloads { get; set; } = 30;

        public List<string> AcceptedExtensions { get; set; } = new List<string>()
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webm",
            ".mp4",
            ".txt"
        };

        public List<string> AcceptedMediaTypes { get; set; } = new List<string>()
        {
            "image/png",
            "image/jpeg",
            "audio/mpeg",
            "audio/vorbis",
            "video/mp4",
            "application/pdf"
        };

        public List<string> ScanTargetsMediaTypes { get; set; } = new List<string>()
        {
            "text/html",
            "text/css",
            "application/javascript"
        };

        public bool AcceptAllFiles { get; set; } = false;

        public List<string> Urls { get; set; } = new List<string>();
    }

    #region Other Classes
    [MessagePackObject(keyAsPropertyName: true)]
    public class WebGUIEndPoint
    {
        public string IP { get; set; } = IPAddress.Loopback.ToString();
        public int Port { get; set; } = 6001;
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class HostListeningEndPoint
    {
        public string IP { get; set; } = IPAddress.Any.ToString();
        public int Port { get; set; } = 6000;
        public string Password { get; set; } = "";
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class HostEndPoint
    {
        public string Hostname { get; set; } = "localhost";
        public int Port { get; set; } = 6000;
        public string Password { get; set; } = "";
        public bool UseHost { get; set; } = false;
    }
    #endregion

}
