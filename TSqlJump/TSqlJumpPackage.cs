using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TSqlJump
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(TSqlJumpPackage.PackageGuidString)]
    public sealed class TSqlJumpPackage : AsyncPackage
    {
        /// <summary>
        /// TSqlJumpPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "a4667fec-850a-4ff1-a300-f040ebf94395";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            //await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);


            //string filePath = Path.Combine(
            //    AppDomain.CurrentDomain.BaseDirectory,
            //    "Templates.toml");

            //var snippets = TSqlJumpTomlParser.Parse(filePath);

            //System.Windows.Forms.MessageBox.Show(
            //    "Okunan snippet sayısı: " + snippets.Count);


            await this.JoinableTaskFactory
            .SwitchToMainThreadAsync(cancellationToken);

            await MenuCommands.InitializeAsync(this);
        }

        #endregion
    }
}
