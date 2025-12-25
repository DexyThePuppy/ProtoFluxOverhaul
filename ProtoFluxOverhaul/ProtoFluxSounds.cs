using FrooxEngine;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using ResoniteModLoader;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
    /// <summary>
    /// Handles wire-related sounds for ProtoFlux
    /// </summary>
    public static class ProtoFluxSounds
    {
        // Cache for shared audio clips (simple approach)
        private static readonly Dictionary<string, StaticAudioClip> sharedAudioClips = new Dictionary<string, StaticAudioClip>();
        
        // List of all sound names we need to preload
        public static readonly string[] SOUND_NAMES = { "Connect", "Delete", "Grab", "NodeCreate", "NodeGrab" };
        
        // Track initialization
        public static bool isInitialized = false;
        public static World currentWorld;

        public static void Initialize(World world)
        {
            if (isInitialized) return;
            currentWorld = world;
            isInitialized = true;
        }

        /// <summary>
        /// Gets the appropriate sound URL from config based on the sound name
        /// </summary>
        public static Uri GetSoundUrl(string soundName)
        {
            Uri result;
            try
            {
                switch (soundName)
                {
                    case "Connect":
                        result = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECT_SOUND);
                        Logger.LogAudio("URL Config", $"Connect sound URL: {result}");
                        return result;
                    case "Delete":
                        result = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.DELETE_SOUND);
                        Logger.LogAudio("URL Config", $"Delete sound URL: {result}");
                        return result;
                    case "Grab":
                        result = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.GRAB_SOUND);
                        Logger.LogAudio("URL Config", $"Grab sound URL: {result}");
                        return result;
                    case "NodeCreate":
                        result = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_CREATE_SOUND);
                        Logger.LogAudio("URL Config", $"NodeCreate sound URL: {result}");
                        return result;
                    case "NodeGrab":
                        result = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_GRAB_SOUND);
                        Logger.LogAudio("URL Config", $"NodeGrab sound URL: {result}");
                        return result;
                    default:
                        throw new ArgumentException($"Unknown sound name: {soundName}");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to get sound URL for {soundName}", e, LogCategory.Audio);
                throw;
            }
        }

        /// <summary>
        /// Creates or gets a shared audio clip for the specified sound
        /// Uses the engine's built-in SlotAssets pattern for optimal performance
        /// </summary>
        public static StaticAudioClip GetSharedAudioClip(World world, string soundName)
        {
            // Check cache first
            if (sharedAudioClips.TryGetValue(soundName, out var existingClip) && !existingClip.IsRemoved)
            {
                Logger.LogAudio("Cache", $"Using cached audio clip for {soundName}");
                return existingClip;
            }

            try
            {
                // Create organized hierarchy under __TEMP
                var tempSlot = world.RootSlot.FindChild("__TEMP") ?? world.RootSlot.AddSlot("__TEMP", false);
                var modSlot = tempSlot.FindChild("ProtoFluxOverhaul") ?? tempSlot.AddSlot("ProtoFluxOverhaul", false);
                var userSlot = modSlot.FindChild(world.LocalUser.UserName) ?? modSlot.AddSlot(world.LocalUser.UserName, false);
                var soundsSlot = userSlot.FindChild("Sounds") ?? userSlot.AddSlot("Sounds", false);
                var clipSlot = soundsSlot.FindChild(soundName) ?? soundsSlot.AddSlot(soundName, false);

                // Get the sound URL
                var soundUrl = GetSoundUrl(soundName);
                Logger.LogAudio("Debug", $"Creating audio clip for {soundName} with URL: {soundUrl}");

                // Use SlotAssets.AttachAudioClip pattern (optimized by engine)
                var clip = clipSlot.AttachAudioClip(soundUrl, getExisting: true);
                
                if (clip == null)
                {
                    Logger.LogError($"Failed to create audio clip for {soundName} with URL {soundUrl}", null, LogCategory.Audio);
                    return null;
                }
                
                // Ensure cleanup when user leaves
                userSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = world.LocalUser;

                // Cache it
                sharedAudioClips[soundName] = clip;
                Logger.LogAudio("Cache", $"Created shared audio clip for {soundName} in {clipSlot.Name} under {clipSlot.Parent?.Name ?? "unknown"}");
                
                return clip;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating shared audio clip for {soundName}", ex, LogCategory.Audio);
                return null;
            }
        }

        /// <summary>
        /// Plays a sound using engine's optimized PlayOneShot pattern
        /// </summary>
        public static void PlaySoundAndCleanup(World world, float3 position, string soundName)
        {
            if (!isInitialized) Initialize(world);
            if (!SOUND_NAMES.Contains(soundName))
            {
                Logger.DebugLog($"Unknown sound name: {soundName}", Logger.LogLevel.Error, Logger.LogCategory.Audio);
                return;
            }

            Logger.LogAudio("PlaySound", $"Playing {soundName} at position {position}");

            try
            {
                // Get shared audio clip (creates if needed) - ensure it's in the same world
                var sharedClip = GetSharedAudioClip(world, soundName);
                
                if (sharedClip == null)
                {
                    Logger.LogError($"Failed to get shared audio clip for {soundName}", null, LogCategory.Audio);
                    return;
                }
                
                // Verify the clip is in the correct world
                if (sharedClip.World != world)
                {
                    Logger.LogError($"Audio clip world mismatch: clip is in {sharedClip.World?.Name ?? "null"}, target world is {world?.Name ?? "null"}", null, LogCategory.Audio);
                    return;
                }
                
                // Use engine's PlayOneShot pattern for optimal performance
                // This automatically handles AudioOutput creation, StoppedPlayableCleaner, etc.
                var audioOutput = world.PlayOneShot(position, sharedClip, 
                                                   volume: ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.AUDIO_VOLUME), 
                                                   spatialize: true, 
                                                   global: false, 
                                                   speed: 1f, 
                                                   parentUnder: null,
                                                   distanceSpace: AudioDistanceSpace.Local, 
                                                   localUserOnly: false);

                // Configure distance settings using config values
                audioOutput.MinDistance.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MIN_DISTANCE);
                audioOutput.MaxDistance.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MAX_DISTANCE);

                Logger.LogAudio("Playback", $"Sound {soundName} started using PlayOneShot");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error playing sound {soundName}", ex, Logger.LogCategory.Audio);
            }
        }

        public static void OnWireConnected(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WIRE_SOUNDS)) return;
            Logger.LogWire("Connect", "Playing connect sound");
            PlaySoundAndCleanup(world, position, "Connect");
        }

        public static void OnWireDeleted(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WIRE_SOUNDS)) return;
            Logger.LogWire("Delete", "Playing delete sound");
            PlaySoundAndCleanup(world, position, "Delete");
        }

        public static void OnWireGrabbed(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WIRE_SOUNDS)) return;
            Logger.LogWire("Grab", "Playing grab sound");
            PlaySoundAndCleanup(world, position, "Grab");
        }

        /// <summary>
        /// Called when a ProtoFlux node is created/spawned
        /// </summary>
        public static void OnNodeCreated(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_SOUNDS)) return;
            Logger.LogNode("Create", "Playing node creation sound");
            PlaySoundAndCleanup(world, position, "NodeCreate");
        }

        /// <summary>
        /// Called when a ProtoFlux node is grabbed
        /// </summary>
        public static void OnNodeGrabbed(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_SOUNDS)) return;
            Logger.LogNode("Grab", "Playing node grab sound");
            PlaySoundAndCleanup(world, position, "NodeGrab");
        }
    }
} 
