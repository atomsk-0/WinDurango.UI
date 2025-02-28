using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using WinDurango.UI.Dialogs;
using WinUI3Localizer;
using static WinDurango.UI.Localization.Locale;

namespace WinDurango.UI.Utils
{
    // do not touch or you will combust from all the glue holding this together
    // this class sucks so much that we literally had to stop bc we didn't know how to rewrite a function and make it still work with the UI stuff
    // update: this was fixed same commit.
#nullable enable
    public class ManifestInfo
    {
        public string? DisplayName { get; set; }
        public string? PublisherDisplayName { get; set; }
        public string? Logo { get; set; }
        public string? Description { get; set; }
    }
    public abstract class Packages
    {
        // TODO: Make these methods not use the GUI, instead just throw an exception and catch it in the area where the method is actually invoked.
        
        /// <summary>
        /// Gets all the installed UWP packages on the system
        /// </summary>
        public static IEnumerable<Package> GetInstalledPackages()
        {
            string? sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid == null)
            {
                Logger.WriteError("Sid is null in Package.GetInstalledPackages, returning empty");
                return [];
            }

            PackageManager pm = new();
            return pm.FindPackagesForUser(sid);
        }

        /// <summary>
        /// Gets some properties from the provided AppxManifest
        /// </summary>
        public static ManifestInfo GetPropertiesFromManifest(string manifestPath)
        {
            ManifestInfo manifestInfo = new();

            if (!File.Exists(manifestPath))
                return manifestInfo;

            string manifest;
            using (var stream = File.OpenRead(manifestPath))
            {
                var reader = new StreamReader(stream);
                manifest = reader.ReadToEnd();
            }

            XDocument doc = XDocument.Parse(manifest);
            XElement? package = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Package");

            XElement? properties = package?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Properties");

            manifestInfo.Logo = properties?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Logo")?.Value;
            manifestInfo.DisplayName = properties?.Descendants().FirstOrDefault(e => e.Name.LocalName == "DisplayName")?.Value;
            manifestInfo.PublisherDisplayName = properties?.Descendants().FirstOrDefault(e => e.Name.LocalName == "PublisherDisplayName")?.Value;
            manifestInfo.Description = properties?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value;
            return manifestInfo;
        }

        /// <summary>
        /// Installs an Xbox Package with the folder given to it
        /// </summary>
        /// <remarks>
        /// This is simply meant for being able to pass a folder containing the Mount directory.
        /// </remarks>
        public static async Task InstallXPackageAsync(string dir, ProgressController controller, bool addInstalledPackage = true)
        {
            string mountDir = Path.Combine(dir, "Mount");

            if (!Directory.Exists(mountDir))
            {
                await new NoticeDialog(GetLocalizedText($"/Errors/NotFound", mountDir), "Error").ShowAsync();
                return;
            }

            await InstallPackageAsync(new Uri(mountDir + "\\AppxManifest.xml", UriKind.Absolute), controller, addInstalledPackage);
        }

        /// <summary>
        /// Gets a Package's splash screen, if none is found... it returns null.
        /// </summary>
        /// <remarks>
        /// It checks the package's AppxManifest for Package.Applications.Application.VisualElements.SplashScreen.Image
        /// </remarks>
        public static string GetSplashScreenPath(Package pkg)
        {
            try
            {
                string installPath = pkg.InstalledPath;
                string manifestPath = Path.Combine(installPath, "AppxManifest.xml");

                if (!File.Exists(manifestPath))
                    return null;

                string manifest;
                using (var stream = File.OpenRead(manifestPath))
                {
                    var reader = new StreamReader(stream);
                    manifest = reader.ReadToEnd();
                }

                XDocument doc = XDocument.Parse(manifest);
                XElement package = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Package");
                if (package == null) return null;

                XElement applications = package.Descendants().FirstOrDefault(e => e.Name.LocalName == "Applications");
                if (applications == null) return null;

                XElement application = applications.Descendants().FirstOrDefault(e => e.Name.LocalName == "Application");
                if (application == null) return null;

                XElement visualElements = application.Descendants().FirstOrDefault(e => e.Name.LocalName == "VisualElements");
                if (visualElements == null) return null;

                XElement splashScreen = visualElements.Descendants().FirstOrDefault(e => e.Name.LocalName == "SplashScreen");
                if (splashScreen == null) return null;

                string imagePath = splashScreen.Attribute("Image")?.Value;
                if (imagePath == null) return null;

                string splashScreenPath = Path.Combine(installPath, imagePath);
                return splashScreenPath;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Installs a Package with the provided AppxManifest
        /// </summary>
        public static async Task<string> InstallPackageAsync(Uri appxManifestUri, ProgressController controller, bool addInstalledPackage = true)
        {
            string manifestPath = Uri.UnescapeDataString(appxManifestUri.AbsolutePath);

            if (!File.Exists(manifestPath))
            {
                throw new Exception(GetLocalizedText("/Errors/NotFound", manifestPath));
            }

            Logger.WriteInformation($"Installing package \"{manifestPath}\"...");
            PackageManager pm = new();
            try
            {
                Logger.WriteInformation($"Reading manifest...");
                controller?.UpdateText(GetLocalizedText("/Packages/ReadingManifest"));
                string manifest;
                await using (var stream = File.OpenRead(manifestPath))
                {
                    StreamReader reader = new StreamReader(stream);
                    manifest = await reader.ReadToEndAsync();
                }

                XDocument doc = XDocument.Parse(manifest);
                XElement? package = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Package");
                XElement? identity = package?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity");

                string? pkgName = identity?.Attribute("Name")?.Value;
                string? pkgPublisher = identity?.Attribute("Publisher")?.Value;

                controller?.UpdateProgress(20.0);
                controller?.UpdateText(GetLocalizedText("/Ui/CheckingInstallStatus", pkgName));
                string? sid = WindowsIdentity.GetCurrent().User?.Value;
                IEnumerable<Package>? installedPackages = await Task.Run(() => pm.FindPackagesForUser(sid, pkgName, pkgPublisher));

                if (installedPackages.Any())
                {
                    Logger.WriteError($"{pkgName} is already installed.");
                    throw new Exception(GetLocalizedText("/Errors/AlreadyInstalled", pkgName));
                }


                controller?.UpdateProgress(40.0);
                controller?.UpdateText(GetLocalizedText("/Packages/InstallingPackage", pkgName));
                Logger.WriteInformation($"Registering...");
                await pm.RegisterPackageAsync(appxManifestUri, null, DeploymentOptions.DevelopmentMode);

                controller?.UpdateProgress(60.0);

                controller?.UpdateText(GetLocalizedText("/Packages/GettingAppInfo"));
                Package recentPkg = GetMostRecentInstalledPackage();

                if (addInstalledPackage)
                {
                    controller?.UpdateText(GetLocalizedText("/Ui/UpdatingAppList"));
                    controller?.UpdateProgress(80.0);
                    App.InstalledPackages.AddPackage(recentPkg);
                    controller?.UpdateProgress(90.0);
                    App.MainWindow.ReloadAppList();
                    controller?.UpdateProgress(100.0);
                }
                else
                {
                    controller?.UpdateProgress(100.0);
                }

                Logger.WriteInformation($"{recentPkg.Id.Name} was installed.");
                return recentPkg.Id.FamilyName;
            }
            catch (Exception e)
            {
                // we're fucked :(
                Logger.WriteError($"{appxManifestUri} failed to install");
                Logger.WriteException(e);
                throw new Exception(
                    GetLocalizedText("/Packages/PackageInstallFailedEx", appxManifestUri, e.Message), e);
            }
        }

        /// <summary>
        /// Uninstalls a package
        /// </summary>
        public static async Task RemovePackage(Package package, ProgressController controller)
        {
            Logger.WriteError($"Uninstalling {package.DisplayName}...");
            PackageManager pm = new();
            try
            {
                var undeployment = await pm.RemovePackageAsync(package.Id.FullName, RemovalOptions.PreserveApplicationData);

                controller.UpdateProgress(50.0);
                App.InstalledPackages.RemovePackage(package);
                controller.UpdateProgress(100.0);
                App.MainWindow.ReloadAppList();
            }
            catch (Exception ex)
            {
                Logger.WriteError($"{package.DisplayName} failed to uninstall");
                Logger.WriteException(ex);
                throw new Exception(GetLocalizedText("/Packages/PackageUninstallFailedEx", package.DisplayName, ex.Message), ex);
            }
        }

        /// <summary>
        /// Gets a Package by it's Family Name
        /// </summary>
        public static Package GetPackageByFamilyName(string familyName)
        {
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser(null, familyName);

            return packages == null || !packages.Any() ? null : packages.First();
        }

        /// <summary>
        /// Gets the most recent installed package
        /// </summary>
        public static Package GetMostRecentInstalledPackage()
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            var pm = new PackageManager();
            var packages = pm.FindPackagesForUser(sid);

            if (!packages.Any())
                return null;

            var newestPackage = packages.Last();

            return newestPackage;
        }
    }
}