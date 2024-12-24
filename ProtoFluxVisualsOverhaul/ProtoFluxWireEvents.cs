using FrooxEngine;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;

namespace ProtoFluxVisualsOverhaul
{
    /// <summary>
    /// Handles wire-related events and plays corresponding sounds
    /// </summary>
    public class ProtoFluxWireEvents : Component
    {
        public readonly SyncRef<ProtoFluxWireManager> TrackedWire = new SyncRef<ProtoFluxWireManager>();
        private bool wasConnected = false;
        private bool wasDestroyed = false;
        private bool wasInteracting = false;
        private IChangeable lastConnectPoint = null;
        private float lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.1f; // 100ms debounce

        protected override void OnAttach()
        {
            base.OnAttach();
            ProtoFluxSounds.Initialize(World);
            ProtoFluxVisualsOverhaul.Msg("🎵 Wire events component attached and initialized!");

            // Initialize wire state and subscriptions
            var wire = TrackedWire.Target;
            if (wire != null)
            {
                wire.ConnectPoint.Changed += OnConnectPointRefChanged;
                UpdateConnectPointState(wire.ConnectPoint.Target, true);
            }

            // Subscribe to wire changes
            TrackedWire.Changed += OnWireChanged;
        }

        private void OnWireChanged(IChangeable changeable)
        {
            var oldWire = (changeable as SyncRef<ProtoFluxWireManager>)?.Target;
            if (oldWire != null)
            {
                oldWire.ConnectPoint.Changed -= OnConnectPointRefChanged;
            }

            var wire = TrackedWire.Target;
            if (wire != null)
            {
                ProtoFluxVisualsOverhaul.Msg("🔄 Wire reference changed, updating state...");
                wire.ConnectPoint.Changed += OnConnectPointRefChanged;
            }
        }

        private void OnConnectPointRefChanged(IChangeable changeable)
        {
            // Debounce the updates
            float currentTime = (float)World.Time.WorldTime;
            if (currentTime - lastUpdateTime < UPDATE_INTERVAL)
            {
                return;
            }
            lastUpdateTime = currentTime;

            var wire = TrackedWire.Target;
            if (wire == null) return;

            UpdateConnectPointState(wire.ConnectPoint.Target, false);
        }

        private void UpdateConnectPointState(Slot connectPoint, bool isInitializing)
        {
            // Unsubscribe from old connect point
            if (lastConnectPoint != null)
            {
                lastConnectPoint.Changed -= OnConnectPointChanged;
            }

            // Subscribe to new connect point
            if (connectPoint != null)
            {
                bool isConnected = connectPoint.Parent?.Name == "Connector";
                
                // Handle state changes
                if (isConnected && !wasConnected && !isInitializing)
                {
                    ProtoFluxVisualsOverhaul.Msg("🔌 Wire connected!");
                    OnWireConnected(TrackedWire.Target);
                }
                else if (!isConnected && wasConnected)
                {
                    ProtoFluxVisualsOverhaul.Msg("🔌 Wire disconnected!");
                }

                wasConnected = isConnected;
                lastConnectPoint = connectPoint;
                connectPoint.Changed += OnConnectPointChanged;
                
                // Only log state changes when they actually happen
                if (wasConnected != isConnected)
                {
                    ProtoFluxVisualsOverhaul.Msg($"🔌 Updated wire state - Connected: {wasConnected}");
                }
            }
            else
            {
                wasConnected = false;
                lastConnectPoint = null;
                ProtoFluxVisualsOverhaul.Msg("🔌 Wire disconnected (no connect point)");
            }
        }

        private void OnConnectPointChanged(IChangeable changeable)
        {
            // Debounce the updates
            float currentTime = (float)World.Time.WorldTime;
            if (currentTime - lastUpdateTime < UPDATE_INTERVAL)
            {
                return;
            }
            lastUpdateTime = currentTime;

            var wire = TrackedWire.Target;
            if (wire == null) return;

            var connectPoint = wire.ConnectPoint.Target;
            if (connectPoint == null) return;

            bool isConnected = connectPoint.Parent?.Name == "Connector";

            // Handle state changes
            if (isConnected && !wasConnected)
            {
                ProtoFluxVisualsOverhaul.Msg("🔌 Wire connected!");
                OnWireConnected(wire);
            }
            else if (!isConnected && wasConnected)
            {
                ProtoFluxVisualsOverhaul.Msg("🔌 Wire disconnected!");
            }

            wasConnected = isConnected;
        }

        protected override void OnChanges()
        {
            base.OnChanges();

            var wire = TrackedWire.Target;
            if (wire == null) return;

            // Check if wire is being destroyed
            if (wire.DeleteHighlight.Value && !wasDestroyed)
            {
                ProtoFluxVisualsOverhaul.Msg("🗑️ Wire being destroyed!");
                OnWireDestroyed(wire);
                wasDestroyed = true;
                return;
            }

            // Reset destroyed state if wire is no longer being deleted
            if (!wire.DeleteHighlight.Value)
            {
                wasDestroyed = false;
            }

            // Check if wire is being interacted with
            bool isInteracting = wire.Slot.Name.Contains("TempWire");
            if (isInteracting && !wasInteracting)
            {
                ProtoFluxVisualsOverhaul.Msg("✋ Wire grabbed!");
                OnWireGrabbed(wire);
            }
            wasInteracting = isInteracting;
        }

        private void OnWireConnected(ProtoFluxWireManager wire)
        {
            if (wire?.Slot != null)
            {
                // Delay the sound slightly to ensure proper timing
                StartTask(async () => {
                    await default(NextUpdate);
                    if (wire?.Slot != null && !wire.IsRemoved)
                    {
                        ProtoFluxSounds.OnWireConnected(World, wire.Slot.GlobalPosition);
                    }
                });
            }
        }

        private void OnWireDestroyed(ProtoFluxWireManager wire)
        {
            if (wire?.Slot != null)
            {
                // Play delete sound immediately
                ProtoFluxSounds.OnWireDeleted(World, wire.Slot.GlobalPosition);
            }
        }

        private void OnWireGrabbed(ProtoFluxWireManager wire)
        {
            if (wire?.Slot != null)
            {
                ProtoFluxSounds.OnWireGrabbed(World, wire.Slot.GlobalPosition);
            }
        }

        protected override void OnDestroy()
        {
            // Unsubscribe from all events
            var wire = TrackedWire.Target;
            if (wire != null)
            {
                wire.ConnectPoint.Changed -= OnConnectPointRefChanged;
            }

            TrackedWire.Changed -= OnWireChanged;
            if (lastConnectPoint != null)
            {
                lastConnectPoint.Changed -= OnConnectPointChanged;
            }
            
            base.OnDestroy();
            ProtoFluxVisualsOverhaul.Msg("🗑️ Wire events component destroyed!");
        }
    }
} 
