using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaveSaver
{
    internal static class Checkpoint
    {
        private static int _suppressionDepth;
        private static FieldSystem _pendingField;
        private static TempSaveData _pendingRun;
        private static int _pendingStage;
        private static string _pendingMap;
        private static string _pendingQueue;

        private static bool HasPending
        {
            get
            {
                if (_pendingQueue == null)
                    return false;
                if (_pendingField != null && _pendingField == FieldSystem.instance &&
                    ReferenceEquals(_pendingRun, PlayData.TSavedata) && PlayData.GameStarted &&
                    !PlayData.StoryPart && _pendingRun.StageNum == _pendingStage &&
                    _pendingRun.NowStageMapKey == _pendingMap)
                    return true;
                ClearPending();
                return false;
            }
        }

        internal static bool ShouldPreserve =>
            SceneManager.GetActiveScene().name == "Battle" || BattleSystem.instance != null ||
            IsTransitioning;

        internal static bool IsTransitioning =>
            (PlayData.GameStarted && FieldSystem.instance != null && FieldSystem.instance.BattleStartFlag) || HasPending;

        internal static bool CanCapture(FieldSystem field)
        {
            return field != null && !field.BattleStartFlag && BattleSystem.instance == null &&
                   SaveManager.savemanager != null && PlayData.GameStarted && !PlayData.StoryPart &&
                   PlayData.TSavedata != null && !PlayData.TSavedata.IsLoaded &&
                   SceneManager.GetActiveScene().name == "Field";
        }

        internal static bool Capture(FieldSystem field, bool swordSanctuary = false)
        {
            if (_suppressionDepth != 0 || HasPending || !CanCapture(field))
                return false;

            var run = PlayData.TSavedata;
            var originalSwordSanctuary = run.SwordSanctuary;
            try
            {
                if (swordSanctuary)
                    run.SwordSanctuary = true;
                SaveManager.savemanager.ProgressOneSave();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSaver: failed to save progress before battle: " + e);
                return false;
            }
            finally
            {
                run.SwordSanctuary = originalSwordSanctuary;
            }
        }

        internal static bool BeginPending(string queue)
        {
            if (HasPending && _pendingQueue != queue)
                ClearPending();
            if (!Capture(FieldSystem.instance))
                return false;
            _pendingField = FieldSystem.instance;
            _pendingRun = PlayData.TSavedata;
            _pendingStage = _pendingRun.StageNum;
            _pendingMap = _pendingRun.NowStageMapKey;
            _pendingQueue = queue;
            return true;
        }

        internal static bool ConsumePending(string queue)
        {
            var matches = HasPending && _pendingQueue == queue;
            ClearPending();
            return matches;
        }

        internal static void ClearPending()
        {
            _pendingField = null;
            _pendingRun = null;
            _pendingQueue = null;
        }

        internal static void Suppress() => _suppressionDepth++;
        internal static void EndSuppression() => _suppressionDepth--;

        internal static void Reset()
        {
            _suppressionDepth = 0;
            ClearPending();
        }
    }
}
