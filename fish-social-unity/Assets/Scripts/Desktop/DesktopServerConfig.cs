using System;
using System.IO;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Resolves the Node server base URL for Steam login, REST, and Socket.IO.
    /// Priority: FISH_SOCIAL_SERVER_URL &gt; server.json beside the EXE &gt; localhost default.
    /// </summary>
    public static class DesktopServerConfig
    {
        public const string DefaultServerBaseUrl = "http://localhost:3001";
        public const string EnvVarName = "FISH_SOCIAL_SERVER_URL";
        public const string ConfigFileName = "server.json";

        [Serializable]
        sealed class ServerJsonDto
        {
            public string serverBaseUrl;
        }

        public static string Resolve(out string source)
        {
            var envRaw = Environment.GetEnvironmentVariable(EnvVarName);
            if (TryNormalize(envRaw, out var fromEnv))
            {
                source = "env:" + EnvVarName;
                return fromEnv;
            }
            if (!string.IsNullOrWhiteSpace(envRaw))
            {
                Debug.LogWarning(
                    "[DesktopShell] Invalid " + EnvVarName +
                    " value; falling back to " + ConfigFileName + " or default.");
            }

            var configPath = ResolveConfigFilePath();
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var dto = JsonUtility.FromJson<ServerJsonDto>(json);
                    if (TryNormalize(dto != null ? dto.serverBaseUrl : null, out var fromFile))
                    {
                        source = "file:" + configPath;
                        return fromFile;
                    }

                    Debug.LogWarning(
                        "[DesktopShell] " + ConfigFileName +
                        " is missing a valid serverBaseUrl; using default.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[DesktopShell] Failed to read " + configPath + ": " + ex.Message);
                }
            }

            source = "default";
            return DefaultServerBaseUrl;
        }

        public static string ResolveConfigFilePath()
        {
            // Standalone: <exeDir>/<game>_Data → parent is EXE directory.
            // Editor: <project>/Assets → parent is Unity project root.
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", ConfigFileName));
        }

        public static bool TryWriteServerBaseUrl(string raw, out string normalized, out string error)
        {
            normalized = null;
            error = null;
            if (!TryNormalize(raw, out normalized))
            {
                error = "服务器地址无效，请使用 http://主机:端口 格式。";
                return false;
            }

            try
            {
                var path = ResolveConfigFilePath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = "{\n  \"serverBaseUrl\": \"" + normalized + "\"\n}\n";
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                error = "写入 server.json 失败：" + ex.Message;
                return false;
            }
        }

        public static bool TryNormalize(string raw, out string serverBaseUrl)
        {
            serverBaseUrl = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var trimmed = raw.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;
            if (string.IsNullOrEmpty(uri.Host))
                return false;

            serverBaseUrl = trimmed;
            return true;
        }
    }
}
