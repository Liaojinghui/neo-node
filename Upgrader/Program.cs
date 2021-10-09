// Copyright (C) 2021 The Neo Project.
// 
// The Upgrader is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
namespace Upgrader
{
    class Program
    {
        static void Main(string[] args)
        {
            var pluginPath = "Plugins";
            if (!Directory.Exists(pluginPath)) return;

            var fileEntries = Directory.GetFiles(pluginPath, "*.dll").Select(file => Path.GetFileNameWithoutExtension(file)).ToList();

            GetLatestVersion(fileEntries);
        }


        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <param name="pluginName">the name of the plugin</param>
        /// <param name="byForce">whether to overwrite existing plugin files</param>
        private static void UpdatePlugin(string pluginName, string version)
        {
            //var recentVersion = // https://api.github.com/repos/neo-project/neo-modules/releases/latest;

            HttpWebRequest request = WebRequest.CreateHttp($"https://github.com/neo-project/neo-modules/releases/download/v{version}/{pluginName}.zip");
            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex) when (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
            {
                version = version.Substring(1);
                Version version_core = Version.Parse(version);
                request = WebRequest.CreateHttp("https://api.github.com/repos/neo-project/neo-modules/releases");
                request.UserAgent = "Foo";
                using HttpWebResponse response_api = (HttpWebResponse)request.GetResponse();
                using Stream stream = response_api.GetResponseStream();
                using StreamReader reader = new(stream);
                JObject releases = JObject.Parse(reader.ReadToEnd());
                JObject asset = releases.GetArray()
                    .Where(p => !p["tag_name"].GetString().Contains('-'))
                    .Select(p => new
                    {
                        Version = Version.Parse(p["tag_name"].GetString().TrimStart('v')),
                        Assets = p["assets"].GetArray()
                    })
                    .OrderByDescending(p => p.Version)
                    .First(p => p.Version <= version_core).Assets
                    .FirstOrDefault(p => p["name"].GetString() == $"{pluginName}.zip");
                if (asset is null) throw new Exception("Plugin doesn't exist.");
                request = WebRequest.CreateHttp(asset["browser_download_url"].GetString());
                response = (HttpWebResponse)request.GetResponse();
            }
            using (response)
            {
                using Stream stream = response.GetResponseStream();
                using ZipArchive zip = new(stream, ZipArchiveMode.Read);
                try
                {
                    zip.ExtractToDirectory(".", true);

                    Console.WriteLine($"{pluginName} updated successfully");
                }
                catch (IOException)
                {
                }
            }
        }

        private static void GetLatestVersion(List<string> ddlFiles)
        {
            HttpWebRequest request = WebRequest.CreateHttp($"https://api.github.com/repos/neo-project/neo-modules/releases/latest");
            request.UserAgent = "Foo";
            request.Accept = "application/json";
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using StreamReader stream = new StreamReader(response.GetResponseStream());
                string json = stream.ReadToEnd();
                var objects = JObject.Parse(json); // parse as array
                var version = objects["name"].GetString();
                var assets = objects["assets"].GetArray();
                foreach (var plugin in assets)
                {
                    var pluginName = Path.GetFileNameWithoutExtension(plugin["name"].GetString());
                    if (ddlFiles.Contains(pluginName))
                        UpdatePlugin(pluginName, version);

                }
            }
            catch (WebException ex) when (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
        }

    }
}