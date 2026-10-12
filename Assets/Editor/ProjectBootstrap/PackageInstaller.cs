using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    // Installs and removes a fixed set of packages via the PackageManager Client API, headless-safe.
    public static class PackageInstaller
    {
        static readonly string[] PackagesToAdd =
        {
            "com.unity.splines",
            // Shared tunnel package, built in Vectagon. Relative to this project's Packages folder.
            "file:../../vectagon/Packages/com.tomkail.tunnel",
        };

        static readonly string[] PackagesToRemove = { };

        const double TimeoutSeconds = 600;

        static AddAndRemoveRequest _request;
        static double _deadline;

        // Invoke with: -executeMethod ProjectBootstrap.PackageInstaller.Install  (NO -quit)
        public static void Install()
        {
            Debug.Log($"[PackageInstaller] Adding: {string.Join(", ", PackagesToAdd)}; removing: {string.Join(", ", PackagesToRemove)}");
            _request = Client.AddAndRemove(packagesToAdd: PackagesToAdd, packagesToRemove: PackagesToRemove);
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null) return;
            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }
            EditorApplication.update -= Poll;
            if (_request.Status == StatusCode.Success)
            {
                Debug.Log($"[PackageInstaller] Resolved: {string.Join(", ", _request.Result.Select(p => $"{p.name}@{p.version}"))}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PackageInstaller] Failed: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
