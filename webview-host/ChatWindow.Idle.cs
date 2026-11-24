using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace DeepSeekFloat
{
    internal sealed partial class ChatWindow
    {
        // A quick tray round-trip should keep the same page, including its scroll position.
        private readonly Timer idleTimer = new Timer { Interval = 60000 };
        // A separate site lets Chromium release the previous site's renderer while
        // the original tab and its session storage stay alive. Its responses are local.
        private const string IdleDocument = "https://idle.reflex.invalid/";
        private static readonly byte[] EmptyDocument = System.Text.Encoding.UTF8.GetBytes("<!doctype html><link rel=icon href='data:,'>");
        private bool windowActive, wasHidden;
        private int visibilityGeneration;

        private void InitializeIdlePolicy()
        {
            Activated += delegate { windowActive = true; UpdatePageActivity(); };
            Deactivate += delegate { windowActive = false; UpdatePageActivity(); };
            VisibleChanged += delegate { UpdatePageActivity(); };
            SizeChanged += delegate { UpdatePageActivity(); };
            idleTimer.Tick += async delegate {
                idleTimer.Stop();
                await ReleaseIdleHome();
            };
        }

        private bool HiddenFromView { get { return !Visible || WindowState == FormWindowState.Minimized; } }

        private static void RegisterIdleDocument(CoreWebView2 core)
        {
            core.AddWebResourceRequestedFilter(IdleDocument+"*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += delegate(object sender, CoreWebView2WebResourceRequestedEventArgs e) {
                if (!e.Request.Uri.StartsWith(IdleDocument,StringComparison.Ordinal)) return;
                e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(EmptyDocument, false),
                    200, "OK", "Content-Type: text/html; charset=utf-8\r\nContent-Security-Policy: default-src 'none'; img-src data:\r\nReferrer-Policy: no-referrer\r\nCache-Control: no-store");
            };
        }

        private void UpdatePageActivity()
        {
            if (resourcesDisposed || quitting || page == null) return;
            bool hidden = HiddenFromView;
            if (hidden != wasHidden) {
                wasHidden = hidden;
                visibilityGeneration++;
                if (!hidden) idleTimer.Stop();
            }
            if (!page.Initialized || recreateBrowser || browser.IsDisposed) return;
            bool visibilityChanged = page.OwnerHidden != hidden;
            if (visibilityChanged) {
                page.OwnerHidden = hidden;
                var view = browser as DeferredFocusWebView;
                if (view != null) view.SetOwnerVisibility(!hidden);
                else browser.Visible = !hidden;
            }
            if (!hidden && page.ParkedHome) {
                page.ParkedHome = false;
                RestoreIdleHome(page, browser.CoreWebView2);
            }
            bool low = hidden || !windowActive;
            if (!page.ParkedHome && (visibilityChanged || low != page.LowMemory)) {
                try {
                    browser.CoreWebView2.MemoryUsageTargetLevel = low
                        ? CoreWebView2MemoryUsageTargetLevel.Low : CoreWebView2MemoryUsageTargetLevel.Normal;
                    page.LowMemory = low;
                } catch (NotImplementedException) { } // Older Evergreen runtimes.
                  catch (InvalidOperationException) { } // Browser process is exiting.
                  catch (COMException) { }
            }
            if (hidden && !page.ParkedHome && page.Navigated && !idleTimer.Enabled) idleTimer.Start();
        }

        private bool CanReleaseIdleHome(ClientPage owner, int generation)
        {
            return !resourcesDisposed && !quitting && page == owner && generation == visibilityGeneration &&
                HiddenFromView && !recreateBrowser && owner.Navigated && !owner.View.IsDisposed &&
                !owner.DownloadStarted && !ModalOpen && !initializing && !bridgeInFlight && !inserting &&
                !capturing && !OwnedForms.OfType<PopupWindow>().Any(f => !f.IsDisposed);
        }

        private async void RestoreIdleHome(ClientPage owner, CoreWebView2 core)
        {
            try {
                if (core.IsSuspended) owner.LowMemory = false;
                core.Resume();
                owner.View.Show();
                owner.Navigated = false;
                owner.Message = "正在打开 "+owner.Client.Name+"…";
                PresentBrowserState(owner);
                // Replace both history entries so the old website cannot linger in the back/forward cache.
                if (core.Source == IdleDocument)
                    await core.ExecuteScriptAsync("location.replace("+PromptInserter.Json(owner.Client.Home.AbsoluteUri)+")");
                else core.Navigate(owner.Client.Home.AbsoluteUri);
            } catch (Exception error) {
                if (resourcesDisposed || quitting || page != owner || owner.View.IsDisposed) return;
                owner.Error = error.GetType().Name;
                owner.Message = "恢复页面时遇到问题，点此重试";
                PresentBrowserState(owner);
            }
        }

        private async Task SuspendIdleDocument(ClientPage owner, CoreWebView2 core)
        {
            // WinForms still reports a child as visible when its owner is minimized.
            owner.View.Hide();
            try {
                await core.TrySuspendAsync();
                if (!resourcesDisposed && !quitting && !owner.View.IsDisposed && owner == page && !owner.ParkedHome) {
                    if (core.IsSuspended) owner.LowMemory = false;
                    core.Resume();
                    UpdatePageActivity();
                }
            } catch (InvalidOperationException) { }
              catch (COMException) { }
        }

        private async Task ReleaseIdleHome()
        {
            var owner = page;
            int generation = visibilityGeneration;
            if (!CanReleaseIdleHome(owner, generation)) return;
            var core = owner.View.CoreWebView2;
            ulong navigation = owner.NavigationId;
            var home = PromptInserter.Json(owner.Client.Home.AbsoluteUri);
            // Only discard an untouched landing page. A chat, sign-in, transfer or unclear
            // document keeps running at the low memory target, with its renderer intact.
            string script = @"(()=>{
if(location.href!==" + home + @" || document.readyState!=='complete' || window.__reflexUsedPage ||
   window.__reflexAttachment || window.scrollX || window.scrollY) return false;
const visible=e=>e.getBoundingClientRect().width>0 && e.getBoundingClientRect().height>0;
if([...document.querySelectorAll('dialog[open],[role=dialog],[aria-modal=true],iframe,input[type=password],input[type=tel],input[type=email],input[autocomplete=username],input[autocomplete=one-time-code]')].some(visible))return false;
if([...document.querySelectorAll('audio,video')].some(e=>!e.paused) ||
   [...document.querySelectorAll('input[type=file]')].some(e=>e.files.length))return false;
const editors=[...document.querySelectorAll(" + PromptInserter.Json(owner.Client.EditorSelector) + @")];
if(editors.some(" + owner.Client.HasDraftScript + @"))return false;
return editors.some(visible) || [...document.querySelectorAll('button,[role=button],a')].some(e=>visible(e)&&/^(登录|立即登录|登录\s*\/\s*注册|log in|sign in)$/i.test((e.innerText||'').trim()));
})()";
            try {
                if (await core.ExecuteScriptAsync(script) != "true" || !CanReleaseIdleHome(owner, generation)) return;
                if (owner.NavigationId != navigation || core.Source != owner.Client.Home.AbsoluteUri) return;
            } catch (InvalidOperationException) { return; }
              catch (COMException) { return; }

            // Keep the browsing context. Closing the last controller can end session-only
            // cookies, and destroying a tab always loses its per-origin sessionStorage.
            try {
                owner.ParkedHome = true;
                await core.ExecuteScriptAsync("location.replace("+PromptInserter.Json(IdleDocument)+")");
            } catch (InvalidOperationException) { owner.ParkedHome = false; }
              catch (COMException) { owner.ParkedHome = false; }
        }
    }
}
