﻿using System.Linq;
using Newtonsoft.Json;
using CryCrawler.Worker;
using CryCrawler.Network;
using System.Diagnostics;
using System.Collections.Generic;
using System;

namespace CryCrawler.Host
{
    class HostResponder : WebGUIResponder
    {
        protected override string GetPath() => "Host.GUI";
        protected override string ResponsePOST(string filename, string body, out string contentType)
        {
            if (filename == "status")
            {
                contentType = ContentTypes["json"];
                return getStatus();
            }
            else if (filename == "state")
            {
                contentType = ContentTypes["json"];

                // parse content
                var state = JsonConvert.DeserializeObject<StateUpdateRequest>(body);

                return handleStateUpdate(state);
            }
            else if (filename == "config")
            {
                contentType = ContentTypes["json"];

                // parse content
                var config = JsonConvert.DeserializeObject<ConfigUpdateRequest>(body);

                return handleConfigUpdate(config);
            }
            else return base.ResponsePOST(filename, body, out contentType);
        }

        Configuration config;
        WorkManager workManager;
        WorkerManager workerManager;

        public HostResponder(Configuration config, WorkManager workManager, WorkerManager workerManager)
        {
            this.config = config;
            this.workManager = workManager;
            this.workerManager = workerManager;
        }

        string getStatus() => JsonConvert.SerializeObject(new
        {
            IsListening = workerManager.IsListening,
            WorkAvailable = workManager.IsWorkAvailable,
            ConnectedToHost = workManager.ConnectedToHost,
            UsingHost = config.WorkerConfig.HostEndpoint.UseHost,
            HostEndpoint = $"{config.WorkerConfig.HostEndpoint.Hostname}:{config.WorkerConfig.HostEndpoint.Port}",
            ClientId = config.WorkerConfig.HostEndpoint.ClientId,
            WorkCount = workManager.WorkCount,
            CacheCount = workManager.CachedWorkCount,
            CacheCrawledCount = workManager.CachedCrawledWorkCount,
            UsageRAM = Process.GetCurrentProcess().PrivateMemorySize64,
            Clients = workerManager.Clients.Select(x => new
            {
                x.Id,
                x.Online,
                x.IsActive,
                LastConnected = x.LastConnected.ToString("dd.MM.yyyy HH:mm:ss"),
                RemoteEndpoint = x.RemoteEndpoint.ToString()
            }),
            // read local config, not Host provided
            Whitelist = config.WorkerConfig.DomainWhitelist,
            Blacklist = config.WorkerConfig.DomainBlacklist,
            AcceptedExtensions = config.WorkerConfig.AcceptedExtensions,
            AccesptedMediaTypes = config.WorkerConfig.AcceptedMediaTypes,
            ScanTargetMediaTypes = config.WorkerConfig.ScanTargetsMediaTypes,
            SeedUrls = config.WorkerConfig.Urls,
            AllFiles = config.WorkerConfig.AcceptAllFiles,
            MaxSize = config.WorkerConfig.MaximumAllowedFileSizekB,
            MinSize = config.WorkerConfig.MinimumAllowedFileSizekB
        });

        string handleStateUpdate(StateUpdateRequest req)
        {
            // handle it
            if (req.IsActive == false)
            {
                // stop it if started
                if (workerManager.IsListening) workerManager.Stop();
            }
            else
            {
                // start it if stopped
                if (!workerManager.IsListening) workerManager.Start();
            }

            if (req.ClearCache == true)
            {
                if (config.WorkerConfig.HostEndpoint.UseHost)
                    throw new InvalidOperationException("Can not clear cache when using Host as Url source!");

                if (workerManager.IsListening)
                {
                    workerManager.Stop();
                    workManager.ClearCache();
                    workerManager.Start();
                }
                else
                {
                    workManager.ClearCache();
                }
            }

            return JsonConvert.SerializeObject(new
            {
                Success = true,
                Error = ""
            });
        }

        string handleConfigUpdate(ConfigUpdateRequest req)
        {
            if (config.WorkerConfig.HostEndpoint.UseHost)
                throw new InvalidOperationException("Can not update configuration when using Host as Url source!");

            string error = "";
            try
            {
                // update configuration and save it
                config.WorkerConfig.Urls = req.SeedUrls;
                config.WorkerConfig.AcceptAllFiles = req.AllFiles;
                config.WorkerConfig.DomainWhitelist = req.Whitelist;
                config.WorkerConfig.DomainBlacklist = req.Blacklist;
                config.WorkerConfig.AcceptedExtensions = req.Extensions;
                config.WorkerConfig.AcceptedMediaTypes = req.MediaTypes;
                config.WorkerConfig.ScanTargetsMediaTypes = req.ScanTargets;
                config.WorkerConfig.MaximumAllowedFileSizekB = req.MaxSize;
                config.WorkerConfig.MinimumAllowedFileSizekB = req.MinSize;
                ConfigManager.SaveConfiguration(ConfigManager.LastLoaded);

                // reload seed urls
                workManager.ReloadUrlSource();

                // TODO: send work to other clients!!!
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return JsonConvert.SerializeObject(new
            {
                Success = string.IsNullOrEmpty(error),
                Error = error
            });
        }

        class StateUpdateRequest
        {
            public bool IsActive { get; set; }
            public bool ClearCache { get; set; }
        }

        class ConfigUpdateRequest
        {
            public bool AllFiles { get; set; }
            public List<string> Extensions { get; set; }
            public List<string> MediaTypes { get; set; }
            public List<string> ScanTargets { get; set; }
            public List<string> SeedUrls { get; set; }
            public List<string> Whitelist { get; set; }
            public List<string> Blacklist { get; set; }
            public double MaxSize { get; set; }
            public double MinSize { get; set; }
        }
    }
}
