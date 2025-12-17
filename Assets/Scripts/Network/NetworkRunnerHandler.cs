using Fusion.Sockets;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunnerHandler Instance;

    private INetworkSceneManager sceneManager;
    public NetworkRunner networkRunner;

    public GameObject[] PlayerPrefab;
    public int SelectedPlayer = 0;

    public GameObject GameManagerPrefab;
    public GameObject PanManagerPrefab;

    // [수정 1] 현재 매칭 시도 중인지 확인하는 변수
    private bool _isJoining = false;
    private string _targetLobby = "";
    private int _targetPlayerCount = 2;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }

        networkRunner = GetComponent<NetworkRunner>();
        if (networkRunner == null)
        {
            networkRunner = gameObject.AddComponent<NetworkRunner>();
        }

        sceneManager = networkRunner.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();
        if (sceneManager == null)
        {
            sceneManager = networkRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
    }

    // [수정 2] 바로 게임을 시작하지 않고, 로비에 먼저 접속합니다.
    public void FindOneVsOneMatch(string customLobby = "1vs1")
    {
        _isJoining = true;
        _targetLobby = customLobby;
        _targetPlayerCount = 2;

        // 로비에 접속하여 방 리스트를 받아올 준비를 합니다.
        networkRunner.JoinSessionLobby(SessionLobby.Custom, customLobby);
    }

    public void FindTwoVsTwoMatch(string customLobby = "2vs2")
    {
        _isJoining = true;
        _targetLobby = customLobby;
        _targetPlayerCount = 4;

        networkRunner.JoinSessionLobby(SessionLobby.Custom, customLobby);
    }

    // [수정 3] 로비 접속 후 세션 리스트가 업데이트될 때 호출됩니다. (여기가 핵심!)
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        // 매칭 중이 아니라면 무시
        if (!_isJoining) return;

        // 1. 들어갈 수 있는 방 찾기 (꽉 차지 않았고, 열려 있는 방)
        SessionInfo availableSession = null;
        foreach (var session in sessionList)
        {
            if (session.PlayerCount < session.MaxPlayers && session.IsOpen)
            {
                availableSession = session;
                break; // 빈 방을 찾았으니 루프 종료
            }
        }

        // 2. 방이 있으면 -> Join (참가)
        if (availableSession != null)
        {
            Debug.Log($"Found Session! Joining {availableSession.Name}");
            StartGame(availableSession.Name);
        }
        // 3. 방이 없으면 -> Create (생성)
        else
        {
            Debug.Log("No Session Found. Creating New One.");
            // 세션 이름을 랜덤(Guid)으로 생성하여 겹치지 않게 함
            StartGame(System.Guid.NewGuid().ToString());
        }

        // 중복 실행 방지
        _isJoining = false;
    }

    // [수정 4] StartGame 로직을 분리하여 재사용
    private void StartGame(string sessionName)
    {
        networkRunner.ProvideInput = true;
        networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            Address = NetAddress.Any(),
            Scene = SceneRef.FromIndex(1),
            CustomLobbyName = _targetLobby,
            SessionName = sessionName, // 찾은 방 이름 or 새로운 이름
            PlayerCount = _targetPlayerCount,
            SceneManager = sceneManager,
        });
    }

    // ... (이하 기존 코드와 동일: OnPlayerJoined, OnInput 등) ...

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // ... (기존 코드 유지) ...
        if (runner.SessionInfo.PlayerCount == 1 && runner.IsSharedModeMasterClient)
        {
            runner.SessionInfo.IsOpen = false; // [참고] 게임 시작 시 난입 금지 처리하려면 false, 아니면 true
            runner.Spawn(PanManagerPrefab);
            runner.Spawn(GameManagerPrefab);
        }

        if (player == runner.LocalPlayer)
        {
            var localCharacter = runner.Spawn(PlayerPrefab[SelectedPlayer],
                                    new Vector3(UnityEngine.Random.Range(0f, 3f), 0.75f, UnityEngine.Random.Range(0f, 3f)),
                                    Quaternion.identity, player);
            Debug.Log($"Spawn Character : {runner.LocalPlayer}");
            runner.SetPlayerObject(player, localCharacter);
        }
    }

    // ... (나머지 OnPlayerLeft, OnInput, Callback 함수들은 기존 코드 그대로 사용) ...
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        foreach (var item in runner.ActivePlayers)
        {
            if (item == player)
            {
                runner.Despawn(runner.GetPlayerObject(item));
            }
        }

        if (player == runner.LocalPlayer && runner.IsSharedModeMasterClient)
        {
            foreach (var item in runner.ActivePlayers)
            {
                if (item == player) continue;
                if (runner.IsPlayerValid(item))
                {
                    runner.SetMasterClient(item);
                    break;
                }
            }
        }

        if (runner.IsSharedModeMasterClient)
        {
            GameManager.Instance.WaitingForStart = false;
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // ... (기존 코드 유지) ...
        if (SceneManager.GetActiveScene().buildIndex != 1) return;

        var data = new CharacterInputData();
        VariableJoystick joy;

        data.direction = Vector3.zero;
#if UNITY_EDITOR || PLATFORM_STANDALONE_WIN
        if (Input.GetKey(KeyCode.W)) data.direction += Vector3.forward;
        if (Input.GetKey(KeyCode.A)) data.direction += Vector3.left;
        if (Input.GetKey(KeyCode.S)) data.direction += Vector3.back;
        if (Input.GetKey(KeyCode.D)) data.direction += Vector3.right;
        data.direction.Normalize();
#endif
#if UNITY_ANDROID
        joy = InputManager.Instance.joystick;
        data.direction += new Vector3(joy.Horizontal, 0, joy.Vertical);
#endif
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            // ... (기존 WebGL 코드 유지) ...
            if (Input.GetKey(KeyCode.W)) data.direction += Vector3.forward;
            if (Input.GetKey(KeyCode.A)) data.direction += Vector3.left;
            if (Input.GetKey(KeyCode.S)) data.direction += Vector3.back;
            if (Input.GetKey(KeyCode.D)) data.direction += Vector3.right;
            data.direction.Normalize();
            joy = InputManager.Instance.joystick;
            data.direction += new Vector3(joy.Horizontal, 0, joy.Vertical);
        }

        var charObj = runner.GetPlayerObject(runner.LocalPlayer);
        if (charObj != null)
        {
            data.ability = charObj.GetComponent<Character>().isAbilityPressed;
        }

        input.Set(data);
    }

    // ... (나머지 빈 콜백 함수들은 그대로 두셔도 됩니다) ...
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { SceneManager.LoadScene("Title"); }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner.SessionInfo.PlayerCount == runner.SessionInfo.MaxPlayers)
        {
            StartCoroutine(Wait());
        }
    }

    private IEnumerator Wait()
    {
        yield return new WaitForSeconds(0.5f);
        GameManager.Instance.RpcSetWaitingForStart(true);
    }

    public void OnSceneLoadStart(NetworkRunner runner) { }
}
