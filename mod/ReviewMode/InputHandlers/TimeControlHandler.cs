using System;
using System.Text;
using MelonLoader;
using UnityEngine;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Systems.GameTime;

namespace TISpeech.ReviewMode.InputHandlers
{
    /// <summary>
    /// Handles time control input (pause, speed changes, time status).
    /// These controls work in any Review Mode state since normal keybindings are blocked.
    /// </summary>
    public static class TimeControlHandler
    {
        /// <summary>
        /// Handle time control keys (Space for pause, 1-6 for speed, 7 for status).
        /// </summary>
        /// <returns>True if a time control key was handled.</returns>
        public static bool HandleInput()
        {
            var gameTime = GameTimeManager.Singleton;
            if (gameTime == null)
                return false;

            // Numpad 7 - Read full time status (date, time, speed)
            if (Input.GetKeyDown(KeyCode.Keypad7))
            {
                AnnounceFullTimeStatus();
                return true;
            }

            // Space - Toggle pause
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (gameTime.Paused)
                {
                    // Check if time is blocked (e.g., mission assignments not confirmed)
                    if (gameTime.IsBlocked)
                    {
                        string blockReason = TIPromptQueueState.GetBlockingDetailStr();
                        if (!string.IsNullOrEmpty(blockReason))
                        {
                            TISpeechMod.Speak($"Cannot unpause: {TISpeechMod.CleanText(blockReason)}", interrupt: true);
                        }
                        else
                        {
                            TISpeechMod.Speak("Cannot unpause: time is blocked", interrupt: true);
                        }
                    }
                    else
                    {
                        gameTime.Play();
                        AnnounceTimeState();
                    }
                }
                else
                {
                    gameTime.Pause();
                    TISpeechMod.Speak("Paused", interrupt: true);
                }
                return true;
            }

            // Number keys 1-6 - Set speed directly
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetSpeedAndAnnounce(1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                // Only handle Alpha2 (Numpad 2 is used for navigation in most modes)
                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    SetSpeedAndAnnounce(2);
                    return true;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetSpeedAndAnnounce(3);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                // Only handle Alpha4 (Numpad 4 is used for navigation)
                if (Input.GetKeyDown(KeyCode.Alpha4))
                {
                    SetSpeedAndAnnounce(4);
                    return true;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                // Only handle Alpha5 (Numpad 5 is used for activation)
                if (Input.GetKeyDown(KeyCode.Alpha5))
                {
                    SetSpeedAndAnnounce(5);
                    return true;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                // Only handle Alpha6 (Numpad 6 is used for navigation)
                if (Input.GetKeyDown(KeyCode.Alpha6))
                {
                    SetSpeedAndAnnounce(6);
                    return true;
                }
            }

            return false;
        }

        private static void SetSpeedAndAnnounce(int speedIndex)
        {
            var gameTime = GameTimeManager.Singleton;
            if (gameTime == null)
                return;

            // Check if time is blocked before trying to set speed
            if (gameTime.IsBlocked && speedIndex > 0)
            {
                string blockReason = TIPromptQueueState.GetBlockingDetailStr();
                if (!string.IsNullOrEmpty(blockReason))
                {
                    TISpeechMod.Speak($"Cannot set speed: {TISpeechMod.CleanText(blockReason)}", interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("Cannot set speed: time is blocked", interrupt: true);
                }
                return;
            }

            gameTime.SetSpeed(speedIndex, pushBeyondCap: false);
            AnnounceTimeState();
        }

        private static void AnnounceTimeState()
        {
            var gameTime = GameTimeManager.Singleton;
            if (gameTime == null)
                return;

            if (gameTime.Paused)
            {
                TISpeechMod.Speak("Paused", interrupt: true);
            }
            else
            {
                var setting = gameTime.CurrentSpeedSetting;
                string speedText = !string.IsNullOrEmpty(setting.description)
                    ? setting.description
                    : $"Speed {gameTime.currentSpeedIndex}";
                TISpeechMod.Speak(speedText, interrupt: true);
            }
        }

        private static void AnnounceFullTimeStatus()
        {
            var sb = new StringBuilder();

            // Get current game date
            try
            {
                var now = TITimeState.Now();
                if (now != null)
                {
                    sb.Append(now.ToCustomDateString());
                }
            }
            catch
            {
                sb.Append("Date unknown");
            }

            // Get speed status
            var gameTime = GameTimeManager.Singleton;
            if (gameTime != null)
            {
                sb.Append(". ");
                if (gameTime.Paused)
                {
                    sb.Append("Paused");
                }
                else
                {
                    var setting = gameTime.CurrentSpeedSetting;
                    string speedText = !string.IsNullOrEmpty(setting.description)
                        ? setting.description
                        : $"Speed {gameTime.currentSpeedIndex}";
                    sb.Append(speedText);
                }
            }

            // Check if in mission phase
            try
            {
                var missionPhase = GameStateManager.MissionPhase();
                if (missionPhase != null && missionPhase.phaseActive)
                {
                    sb.Append(". Mission phase active");
                }
            }
            catch { }

            // Check if time is blocked and why
            if (gameTime != null && gameTime.IsBlocked)
            {
                string blockReason = TIPromptQueueState.GetBlockingDetailStr();
                if (!string.IsNullOrEmpty(blockReason))
                {
                    sb.Append(". Blocked: ");
                    sb.Append(TISpeechMod.CleanText(blockReason));
                }
                else
                {
                    sb.Append(". Time blocked");
                }
            }

            TISpeechMod.Speak(sb.ToString(), interrupt: true);
        }
    }
}
