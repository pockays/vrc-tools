using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.IO;
using VRC.SDKBase.Editor;
using System.Linq;

[Serializable]
public class VRChatAccount
{
    public string username;
    public string password;
    public string description;
    public int useCount;
}

[Serializable]
public class SerializableAccountList
{
    public List<VRChatAccount> accounts = new List<VRChatAccount>();
}

// 添加扩展方法来检查GUI是否已注册
public static class IMGUIContainerExtensions
{
    public static bool isGUIRegistered(this IMGUIContainer container)
    {
        if (container == null) return false;
        var field = typeof(IMGUIContainer).GetField("m_OnGUIHandler",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        var handler = field?.GetValue(container) as Action;
        return handler != null && handler.GetInvocationList().Any(d => d.Method.Name == "OnSDKWindowGUI");
    }
}

[InitializeOnLoad]
public class VRChatAccountManagerPopup
{
    private static List<VRChatAccount> accounts = new List<VRChatAccount>();
    private static AccountListPopupWindow popupWindow;
    private static bool initialized = false;
    private static EditorWindow sdkWindow;
    private static readonly string accountsFilePath;

    static VRChatAccountManagerPopup()
    {
        string dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
            "Unity",
            "VRChatAccountManager"
        );
        accountsFilePath = Path.Combine(dataPath, "accounts.json");
        
        EditorApplication.delayCall += Initialize;
    }

    private static void Initialize()
    {
        if (initialized) return;
        
        Debug.Log($"账号数据文件路径: {accountsFilePath}");
        LoadAccounts();
        initialized = true;

        EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        var currentSDKWindow = windows.FirstOrDefault(w => w.titleContent.text == "VRChat SDK");
        
        // 如果找到了新的SDK窗口，且与当前记录的不同
        if (currentSDKWindow != null && currentSDKWindow != sdkWindow)
        {
            // 清理旧窗口的事件处理
            if (sdkWindow != null)
            {
                var oldContainer = sdkWindow.rootVisualElement.Q<IMGUIContainer>();
                if (oldContainer != null)
                {
                    oldContainer.onGUIHandler -= OnSDKWindowGUI;
                }
            }

            // 更新为新窗口
            sdkWindow = currentSDKWindow;
        }

        // 确保当前窗口有正确的事件处理器
        if (sdkWindow != null)
        {
            var container = sdkWindow.rootVisualElement.Q<IMGUIContainer>();
            if (container != null && !container.isGUIRegistered())
            {
                container.onGUIHandler += OnSDKWindowGUI;
            }
        }

    }

    private static void OnSDKWindowGUI()
    {
        if (sdkWindow == null) return;

        try
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("账号管理", GUILayout.Width(120), GUILayout.Height(30)))
                {
                    ShowAccountPopup();
                }
                GUILayout.FlexibleSpace();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"绘制账号管理按钮时出错: {e.Message}");
        }
    }

    private static void ShowAccountPopup()
    {
        if (popupWindow != null)
        {
            return;
        }

        accounts = accounts.OrderByDescending(a => a.useCount).ToList();

        popupWindow = EditorWindow.GetWindow<AccountListPopupWindow>(true, "账号列表");
        popupWindow.minSize = new Vector2(600, 400);
        popupWindow.Init(accounts, OnAccountSelected, AddNewAccount, RemoveAccount, UpdateAccountPassword);
        popupWindow.ShowUtility();
    }

    private static void OnAccountSelected(VRChatAccount account)
    {
        try
        {
            var controlPanelType = typeof(VRCSdkControlPanel);
            var usernameProperty = controlPanelType.GetProperty("username", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Static);
            var passwordProperty = controlPanelType.GetProperty("password",
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Static);

            if (usernameProperty != null && passwordProperty != null)
            {
                usernameProperty.SetValue(null, account.username);
                passwordProperty.SetValue(null, account.password);

                account.useCount++;
                SaveAccounts();

                if (sdkWindow != null)
                {
                    sdkWindow.Repaint();
                }

                Debug.Log($"已自动填充账号: {account.username}");
            }
            else
            {
                Debug.LogError("未找到用户名或密码字段");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"自动填充账号时出错: {e.Message}\n{e.StackTrace}");
        }

        if (popupWindow != null)
        {
            popupWindow.Close();
            popupWindow = null;
        }
    }

    private static void AddNewAccount(string username, string password, string description)
    {
        accounts.Add(new VRChatAccount 
        { 
            username = username,
            password = password,
            description = description,
            useCount = 0
        });
        SaveAccounts();
    }

    private static void RemoveAccount(int index)
    {
        if (index >= 0 && index < accounts.Count)
        {
            accounts.RemoveAt(index);
            SaveAccounts();
        }
    }

    private static void UpdateAccountPassword(int index, string newPassword)
    {
        if (index >= 0 && index < accounts.Count)
        {
            accounts[index].password = newPassword;
            SaveAccounts();
        }
    }

    private static void SaveAccounts()
    {
        try
        {
            var directory = Path.GetDirectoryName(accountsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var accountList = new SerializableAccountList { 
                accounts = accounts
            };
            string json = JsonUtility.ToJson(accountList, true);
            File.WriteAllText(accountsFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"保存账号数据时出错: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void LoadAccounts()
    {
        try
        {
            if (File.Exists(accountsFilePath))
            {
                string json = File.ReadAllText(accountsFilePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var accountList = JsonUtility.FromJson<SerializableAccountList>(json);
                    accounts = accountList?.accounts ?? new List<VRChatAccount>();
                }
            }
            else
            {
                accounts = new List<VRChatAccount>();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载账号数据时出错: {e.Message}\n{e.StackTrace}");
            accounts = new List<VRChatAccount>();
        }
    }
}

public class AccountListPopupWindow : EditorWindow
{
    private List<VRChatAccount> accounts;
    private Action<VRChatAccount> onAccountSelected;
    private Action<string, string, string> onAddAccount;
    private Action<int> onRemoveAccount;
    private Action<int, string> onUpdatePassword;
    private bool isAddingNew = false;
    private string newUsername = "";
    private string newPassword = "";
    private string newDescription = "";
    private Vector2 scrollPosition;
    private GUIStyle boxStyle;
    private GUIStyle useCountStyle;
    private GUIStyle headerButtonStyle;
    private int accountToDelete = -1;
    private int editingPasswordIndex = -1;
    private string editingPassword = "";

    public void Init(List<VRChatAccount> accounts, 
                    Action<VRChatAccount> onAccountSelected,
                    Action<string, string, string> onAddAccount,
                    Action<int> onRemoveAccount,
                    Action<int, string> onUpdatePassword)
    {
        this.accounts = accounts;
        this.onAccountSelected = onAccountSelected;
        this.onAddAccount = onAddAccount;
        this.onRemoveAccount = onRemoveAccount;
        this.onUpdatePassword = onUpdatePassword;
    }

    void OnEnable()
    {
        boxStyle = new GUIStyle(EditorStyles.helpBox);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        boxStyle.margin = new RectOffset(5, 5, 5, 5);

        useCountStyle = new GUIStyle(EditorStyles.miniLabel);
        useCountStyle.alignment = TextAnchor.UpperLeft;
        useCountStyle.normal.textColor = Color.gray;
        useCountStyle.fontSize = 10;

        headerButtonStyle = new GUIStyle(EditorStyles.miniButton);
        headerButtonStyle.fontSize = 10;
        headerButtonStyle.padding = new RectOffset(4, 4, 2, 2);
    }

    void OnGUI()
    {
        if (boxStyle == null || useCountStyle == null || headerButtonStyle == null)
        {
            OnEnable();
        }

        EditorGUILayout.Space(10);

        if (isAddingNew)
        {
            DrawAddNewAccount();
        }
        else
        {
            if (GUILayout.Button("添加新账号", GUILayout.Height(25)))
            {
                isAddingNew = true;
                newUsername = "";
                newPassword = "";
                newDescription = "";
            }

            EditorGUILayout.Space(5);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            if (accounts != null)
            {
                for (int i = 0; i < accounts.Count; i += 2)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    DrawAccountBox(i);
                    
                    if (i + 1 < accounts.Count)
                    {
                        DrawAccountBox(i + 1);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        if (accountToDelete >= 0)
        {
            string accountInfo = !string.IsNullOrEmpty(accounts[accountToDelete].description) 
                ? $"{accounts[accountToDelete].description} ({accounts[accountToDelete].username})"
                : accounts[accountToDelete].username;

            if (EditorUtility.DisplayDialog("确认删除", 
                $"确定要删除账号 {accountInfo} 吗？", 
                "确定", "取消"))
            {
                onRemoveAccount?.Invoke(accountToDelete);
            }
            accountToDelete = -1;
        }
    }

    private void DrawAccountBox(int index)
    {
        EditorGUILayout.BeginVertical(boxStyle, GUILayout.Width(position.width / 2 - 15));

        // 顶部栏，包含使用次数和按钮
        EditorGUILayout.BeginHorizontal();
        // 使用次数
        GUILayout.Label($"{accounts[index].useCount}", useCountStyle, GUILayout.Width(20));
        GUILayout.FlexibleSpace();

        // 修改密码按钮
        if (editingPasswordIndex == index)
        {
            editingPassword = EditorGUILayout.PasswordField(editingPassword, GUILayout.Width(100));
            if (GUILayout.Button("√", headerButtonStyle, GUILayout.Width(20)))
            {
                if (!string.IsNullOrEmpty(editingPassword))
                {
                    onUpdatePassword?.Invoke(index, editingPassword);
                }
                editingPasswordIndex = -1;
                editingPassword = "";
            }
            if (GUILayout.Button("×", headerButtonStyle, GUILayout.Width(20)))
            {
                editingPasswordIndex = -1;
                editingPassword = "";
            }
        }
        else
        {
            if (GUILayout.Button("密", headerButtonStyle, GUILayout.Width(25)))
            {
                editingPasswordIndex = index;
                editingPassword = "";
            }
        }

        // 删除按钮
        if (GUILayout.Button("删", headerButtonStyle, GUILayout.Width(25)))
        {
            accountToDelete = index;
        }
        EditorGUILayout.EndHorizontal();

        // 账号信息
        if (!string.IsNullOrEmpty(accounts[index].description))
        {
            GUILayout.Label($"描述: {accounts[index].description}", EditorStyles.boldLabel);
        }
        GUILayout.Label($"用户名: {accounts[index].username}");

        // 使用按钮
        if (GUILayout.Button("使用", GUILayout.Height(25)))
        {
            onAccountSelected?.Invoke(accounts[index]);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAddNewAccount()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        
        GUILayout.Label("添加新账号", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        newDescription = EditorGUILayout.TextField("描述", newDescription);
        newUsername = EditorGUILayout.TextField("用户名", newUsername);
        newPassword = EditorGUILayout.PasswordField("密码", newPassword);

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("保存", GUILayout.Height(25)))
        {
            if (!string.IsNullOrEmpty(newUsername) && !string.IsNullOrEmpty(newPassword))
            {
                onAddAccount?.Invoke(newUsername, newPassword, newDescription);
                isAddingNew = false;
            }
        }
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("取消", GUILayout.Height(25)))
        {
            isAddingNew = false;
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    void OnDestroy()
    {
        var field = typeof(VRChatAccountManagerPopup).GetField("popupWindow", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, null);
        }
    }
}
