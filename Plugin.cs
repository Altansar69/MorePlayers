namespace MorePlayers
{
    using BepInEx;
    using BepInEx.Configuration;
    using BepInEx.Logging;
    using HarmonyLib;
    using Photon.Pun;
    using Photon.Realtime;
    using Steamworks.Data;
    using Steamworks;
    using UnityEngine;
    using UnityEngine.UI;
    using System.Threading.Tasks;

    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string modGUID = "Altansar.MorePlayers";
        public const string modName = "MorePlayers";
        public const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static ConfigEntry<int> configMaxPlayers;

        public static ManualLogSource mls;

        void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo($"{modGUID} is now awake!");

            configMaxPlayers = Config.Bind
            (
                "General", 
                "MaxPlayers", 
                10, 
                "The max amount of players allowed in a server"
            );

            harmony.PatchAll(typeof(TryJoiningRoomPatch));
            harmony.PatchAll(typeof(HostLobbyPatch));

            GameObject scrollSetup = new GameObject("ScrollSetup");
            scrollSetup.AddComponent<MakePlayerListScrollable>();
            DontDestroyOnLoad(scrollSetup);
        }

        [HarmonyPatch(typeof(NetworkConnect), "TryJoiningRoom")]
        public class TryJoiningRoomPatch
        {
            static bool Prefix(ref string ___RoomName)
            {
                if (string.IsNullOrEmpty(___RoomName))
                {
                    mls.LogError("RoomName is null or empty, using previous method!");
                    return true;
                }

                if (configMaxPlayers.Value == 0)
                {
                    mls.LogError("The MaxPlayers config is null or empty, using previous method!");
                    return true;
                }

                if (NetworkConnect.instance != null)
                {
                    PhotonNetwork.JoinOrCreateRoom(___RoomName, new RoomOptions
                    {
                        MaxPlayers = configMaxPlayers.Value
                    }, TypedLobby.Default, null);

                    return false;
                }
                else
                {
                    mls.LogError("NetworkConnect instance is null, using previous method!");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SteamManager), "HostLobby")]
        public class HostLobbyPatch
        {
            static bool Prefix()
            {
                HostLobbyAsync();
                return false;
            }

            static async void HostLobbyAsync()
            {
                Debug.Log("Steam: Hosting lobby...");
                Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(configMaxPlayers.Value);

                if (!lobby.HasValue)
                {
                    Debug.LogError("Lobby created but not correctly instantiated.");
                    return;
                }

                lobby.Value.SetPublic();
                lobby.Value.SetJoinable(b: false);
            }
        }

        public class MakePlayerListScrollable : MonoBehaviour
        {
            void Start()
            {
                var menuLobby = GameObject.Find("Menu Page Lobby(Clone)");
                if (menuLobby == null)
                {
                    Debug.LogError("Menu Page Lobby not found!");
                    return;
                }

                var playerList = menuLobby.transform.Find("Player List");
                if (playerList == null)
                {
                    Debug.LogError("Player List not found!");
                    return;
                }

                GameObject scrollView = new GameObject("PlayerListScrollView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
                GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
                GameObject scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));

                scrollView.transform.SetParent(menuLobby.transform, false);
                viewport.transform.SetParent(scrollView.transform, false);
                scrollbarGO.transform.SetParent(scrollView.transform, false);
                playerList.SetParent(viewport.transform, false);

                var scrollRect = scrollView.GetComponent<ScrollRect>();
                scrollRect.viewport = viewport.GetComponent<RectTransform>();
                scrollRect.content = playerList.GetComponent<RectTransform>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 20f;

                var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

                var scrollRectTransform = scrollView.GetComponent<RectTransform>();
                scrollRectTransform.anchorMin = new Vector2(0, 0);
                scrollRectTransform.anchorMax = new Vector2(1, 1);
                scrollRectTransform.offsetMin = Vector2.zero;
                scrollRectTransform.offsetMax = Vector2.zero;

                var viewportRect = viewport.GetComponent<RectTransform>();
                viewportRect.anchorMin = new Vector2(0, 0);
                viewportRect.anchorMax = new Vector2(1, 1);
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;

                var scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
                scrollbarRect.anchorMin = new Vector2(1f, 0f);
                scrollbarRect.anchorMax = new Vector2(1f, 1f);
                scrollbarRect.pivot = new Vector2(1f, 1f);
                scrollbarRect.sizeDelta = new Vector2(20f, 0f);
                scrollbarRect.offsetMin = new Vector2(-20f, 0);
                scrollbarRect.offsetMax = new Vector2(0, 0);

                Debug.Log("Player List made scrollable");
            }
        }
    }
}
