#if ANOMALITH_IL2CPP && DEBUG
using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalithHudNumbers
{
    internal sealed class HudNumbersSmokeTest
    {
        private readonly string runId;
        private readonly string screenshotDirectory;
        private readonly ManualLogSource logger;
        private readonly MonoBehaviour driver;
        private bool enteredGameplay;
        private bool passed;

        private HudNumbersSmokeTest(string runId, string screenshotDirectory, MonoBehaviour driver, ManualLogSource logger)
        {
            this.runId = runId;
            this.screenshotDirectory = screenshotDirectory;
            this.driver = driver;
            this.logger = logger;
            if (!string.IsNullOrEmpty(screenshotDirectory))
                Directory.CreateDirectory(screenshotDirectory);
        }

        internal static HudNumbersSmokeTest FromEnvironment(MonoBehaviour driver, ManualLogSource logger)
        {
            var id = Environment.GetEnvironmentVariable("ANOMALITH_HUD_TEST_ID");
            var screenshots = Environment.GetEnvironmentVariable("ANOMALITH_TEST_SCREENSHOTS");
            return string.IsNullOrEmpty(id) && string.IsNullOrEmpty(screenshots)
                ? null : new HudNumbersSmokeTest(id, screenshots, driver, logger);
        }

        internal void ReportGameplay()
        {
            if (enteredGameplay || string.IsNullOrEmpty(runId))
                return;

            enteredGameplay = true;
            logger.LogInfo($"HUD smoke test {runId} reached gameplay.");
        }

        internal void Observe(TextMeshProUGUI health, Image hpBar, TextMeshProUGUI mana, Image mpBar)
        {
            if (passed || string.IsNullOrEmpty(runId) ||
                !IsVisibleBesideBar(health, hpBar, "HP ") || !IsVisibleBesideBar(mana, mpBar, "MP "))
                return;

            passed = true;
            logger.LogInfo($"HUD smoke test {runId} passed: {health.text}; {mana.text}.");
            Capture("hud-numbers");
        }

        internal void CheckCaptureKey()
        {
            if (!string.IsNullOrEmpty(screenshotDirectory) && Input.GetKeyDown(KeyCode.F6))
                Capture(DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }

        private void Capture(string name)
        {
            if (!string.IsNullOrEmpty(screenshotDirectory))
                driver.StartCoroutine(SaveFrame(Path.Combine(screenshotDirectory, name + ".png")).WrapToIl2Cpp());
        }

        private IEnumerator SaveFrame(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = null;
            try
            {
                frame = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(frame).ToArray());
                logger.LogInfo($"Saved playtest screenshot to {path}.");
            }
            catch (Exception error)
            {
                logger.LogError($"Could not save playtest screenshot: {error.Message}");
            }
            finally
            {
                if (frame != null)
                    UnityEngine.Object.Destroy(frame);
            }
        }

        private static bool IsVisibleBesideBar(TextMeshProUGUI label, Image bar, string prefix)
        {
            if (!label.isActiveAndEnabled || label.raycastTarget || label.canvasRenderer.cull ||
                label.color.a <= 0 || !label.text.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            if (label.canvas == null)
                return false;

            var canvas = label.canvas.rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera,
                label.rectTransform.TransformPoint((Vector3)label.rectTransform.rect.center));
            Vector2 barCenter = RectTransformUtility.WorldToScreenPoint(camera,
                bar.rectTransform.TransformPoint((Vector3)bar.rectTransform.rect.center));
            return center.x > 0 && center.x < Screen.width && center.y > 0 && center.y < Screen.height &&
                center.x < barCenter.x && Mathf.Abs(center.y - barCenter.y) <= 2;
        }
    }
}
#endif
