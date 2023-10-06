using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ProfitablePieces
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    public static class RegisterAndCheckVersion
    {
        private static void Prefix(ZNetPeer peer, ref ZNet __instance)
        {
            // Register version check call
            ProfitablePiecesPlugin.ProfitablePiecesLogger.LogDebug("Registering version RPC handler");
            peer.m_rpc.Register($"{ProfitablePiecesPlugin.ModName}_VersionCheck",
                new Action<ZRpc, ZPackage>(RpcHandlers.RPC_ProfitablePieces_Version));

            // Make calls to check versions
            ProfitablePiecesPlugin.ProfitablePiecesLogger.LogInfo("Invoking version check");
            ZPackage zpackage = new();
            zpackage.Write(ProfitablePiecesPlugin.ModVersion);
            peer.m_rpc.Invoke($"{ProfitablePiecesPlugin.ModName}_VersionCheck", zpackage);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
    public static class VerifyClient
    {
        private static bool Prefix(ZRpc rpc, ZPackage pkg, ref ZNet __instance)
        {
            if (!__instance.IsServer() || RpcHandlers.ValidatedPeers.Contains(rpc)) return true;
            // Disconnect peer if they didn't send mod version at all
            ProfitablePiecesPlugin.ProfitablePiecesLogger.LogWarning(
                $"Peer ({rpc.m_socket.GetHostName()}) never sent version or couldn't due to previous disconnect, disconnecting");
            rpc.Invoke("Error", 3);
            return false; // Prevent calling underlying method
        }

        private static void Postfix(ZNet __instance)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RequestAdminSync",
                new ZPackage());
        }
    }

    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ShowConnectError))]
    public class ShowConnectionError
    {
        private static void Postfix(FejdStartup __instance)
        {
            if (__instance.m_connectionFailedPanel.activeSelf)
            {
                __instance.m_connectionFailedError.fontSizeMax = 25;
                __instance.m_connectionFailedError.fontSizeMin = 15;
                __instance.m_connectionFailedError.text += "\n" + ProfitablePiecesPlugin.ConnectionError;
            }
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
    public static class RemoveDisconnectedPeerFromVerified
    {
        private static void Prefix(ZNetPeer peer, ref ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            // Remove peer from validated list
            ProfitablePiecesPlugin.ProfitablePiecesLogger.LogInfo(
                $"Peer ({peer.m_rpc.m_socket.GetHostName()}) disconnected, removing from validated list");
            _ = RpcHandlers.ValidatedPeers.Remove(peer.m_rpc);
        }
    }

    public static class RpcHandlers
    {
        public static readonly List<ZRpc> ValidatedPeers = new();

        public static void RPC_ProfitablePieces_Version(ZRpc rpc, ZPackage pkg)
        {
            string? version = pkg.ReadString();
            ProfitablePiecesPlugin.ProfitablePiecesLogger.LogInfo("Version check, local: " +
                                                                  ProfitablePiecesPlugin.ModVersion +
                                                                  ",  remote: " + version);
            if (version != ProfitablePiecesPlugin.ModVersion)
            {
                ProfitablePiecesPlugin.ConnectionError =
                    $"{ProfitablePiecesPlugin.ModName} Installed: {ProfitablePiecesPlugin.ModVersion}\n Needed: {version}";
                if (!ZNet.instance.IsServer()) return;
                // Different versions - force disconnect client from server
                ProfitablePiecesPlugin.ProfitablePiecesLogger.LogWarning(
                    $"Peer ({rpc.m_socket.GetHostName()}) has incompatible version, disconnecting");
                rpc.Invoke("Error", 3);
            }
            else
            {
                if (!ZNet.instance.IsServer())
                {
                    // Enable mod on client if versions match
                    ProfitablePiecesPlugin.ProfitablePiecesLogger.LogInfo("Received same version from server!");
                }
                else
                {
                    // Add client to validated list
                    ProfitablePiecesPlugin.ProfitablePiecesLogger.LogInfo(
                        $"Adding peer ({rpc.m_socket.GetHostName()}) to validated list");
                    ValidatedPeers.Add(rpc);
                }
            }
        }
    }
}