// --- START OF FILE どこどこテクスチャWindow.cs ---

using UnityEngine;
using UnityEditor;
using System.Linq;

public class どこどこテクスチャWindow : EditorWindow
{
    // --- 設定項目 ---
    private float circleRadius = 5f;
    private Color markerColor = Color.red; // マーカーの色

    // --- 内部状態 ---
    private Texture2D currentDisplayTexture;
    private Vector2 uvCoord;
    private bool hasUV;
    private Rect texturePreviewRect;
    private GameObject lastSelectedObject;

    // マテリアル選択用
    private Material[] availableMaterials;
    private int selectedMaterialIndex = 0;
    private string[] materialDisplayNames;
    private int lastSelectedMaterialInstanceID = 0;

    // OnSceneGUI パフォーマンス改善用
    private GameObject tempSmrColliderHost;
    private MeshCollider tempSmrCollider;
    private Mesh bakedSmrMesh;

    // デバッグ用フラグ
    private bool enableDebugLog = false;

    // EditorPrefs Keys
    private const string Prefix = "どこどこテクスチャ_"; // キー名の衝突を避けるためのプレフィックス
    private const string CircleRadiusKey = Prefix + "CircleRadius";
    private const string MarkerColorRKey = Prefix + "MarkerColorR";
    private const string MarkerColorGKey = Prefix + "MarkerColorG";
    private const string MarkerColorBKey = Prefix + "MarkerColorB";
    private const string MarkerColorAKey = Prefix + "MarkerColorA";
    private const string EnableDebugLogKey = Prefix + "EnableDebugLog";


    [MenuItem("Tools/どこどこテクスチャ")]
    static void OpenWindow()
    {
        どこどこテクスチャWindow window = GetWindow<どこどこテクスチャWindow>("どこどこテクスチャ");
        window.minSize = new Vector2(300, 450); // 設定項目が増えたので少し高さを増やす
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        if (enableDebugLog) Debug.Log("どこどこテクスチャ: OnEnable called"); // LoadSettingsより前にログ設定を反映したいので先に

        LoadSettings(); // 設定を読み込む

        lastSelectedObject = null;
        UpdateMaterialListIfNeeded(true);
        InitializeSceneGuiResources();
        Repaint();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SaveSettings(); // ウィンドウが閉じられるときに設定を保存
        if (enableDebugLog) Debug.Log("どこどこテクスチャ: OnDisable called");
        CleanupSceneGuiResources();
    }

    private void LoadSettings()
    {
        circleRadius = EditorPrefs.GetFloat(CircleRadiusKey, 5f);
        markerColor.r = EditorPrefs.GetFloat(MarkerColorRKey, Color.red.r);
        markerColor.g = EditorPrefs.GetFloat(MarkerColorGKey, Color.red.g);
        markerColor.b = EditorPrefs.GetFloat(MarkerColorBKey, Color.red.b);
        markerColor.a = EditorPrefs.GetFloat(MarkerColorAKey, Color.red.a);
        enableDebugLog = EditorPrefs.GetBool(EnableDebugLogKey, false);
        if (enableDebugLog) Debug.Log("どこどこテクスチャ: Settings Loaded.");
    }

    private void SaveSettings()
    {
        EditorPrefs.SetFloat(CircleRadiusKey, circleRadius);
        EditorPrefs.SetFloat(MarkerColorRKey, markerColor.r);
        EditorPrefs.SetFloat(MarkerColorGKey, markerColor.g);
        EditorPrefs.SetFloat(MarkerColorBKey, markerColor.b);
        EditorPrefs.SetFloat(MarkerColorAKey, markerColor.a);
        EditorPrefs.SetBool(EnableDebugLogKey, enableDebugLog);
        if (enableDebugLog) Debug.Log("どこどこテクスチャ: Settings Saved.");
    }


    private void InitializeSceneGuiResources()
    {
        if (tempSmrColliderHost == null)
        {
            tempSmrColliderHost = new GameObject("TempSMRCollider_どこどこテクスチャ_Instance");
            tempSmrColliderHost.hideFlags = HideFlags.HideAndDontSave;
        }
        if (tempSmrCollider == null && tempSmrColliderHost != null)
        {
            tempSmrCollider = tempSmrColliderHost.AddComponent<MeshCollider>();
        }
        if (bakedSmrMesh == null)
        {
            bakedSmrMesh = new Mesh { name = "どこどこテクスチャ_BakedSMRMesh" };
        }
        if (tempSmrColliderHost != null) tempSmrColliderHost.SetActive(false);
    }

    private void CleanupSceneGuiResources()
    {
        if (bakedSmrMesh != null) { DestroyImmediate(bakedSmrMesh); bakedSmrMesh = null; }
        if (tempSmrColliderHost != null) { DestroyImmediate(tempSmrColliderHost); tempSmrColliderHost = null; tempSmrCollider = null; }
    }

    private void OnSelectionChange()
    {
        if (enableDebugLog) Debug.Log("どこどこテクスチャ: OnSelectionChange triggered.");
        UpdateMaterialListIfNeeded(true);
        Repaint();
    }

    private void OnFocus()
    {
        if (enableDebugLog) Debug.Log("どこどこテクスチャ: OnFocus triggered.");
        UpdateMaterialListIfNeeded(false); // オブジェクトの内容が変わった可能性を考慮
        Repaint();
    }

    private void UpdateMaterialListIfNeeded(bool forceUpdate)
    {
        GameObject currentSelectedObject = Selection.activeGameObject;
        bool selectionChanged = (currentSelectedObject != lastSelectedObject);

        if (forceUpdate || selectionChanged)
        {
            if (enableDebugLog && selectionChanged) Debug.Log($"どこどこテクスチャ: Selection changed from '{lastSelectedObject?.name}' to '{currentSelectedObject?.name}'. Updating material list.");

            lastSelectedObject = currentSelectedObject;
            availableMaterials = null;
            materialDisplayNames = null;
            selectedMaterialIndex = 0;
            lastSelectedMaterialInstanceID = 0;
            currentDisplayTexture = null;
            hasUV = false;

            if (currentSelectedObject == null) return;

            Renderer rend = currentSelectedObject.GetComponent<Renderer>();
            if (rend == null)
            {
                if (enableDebugLog) Debug.LogWarning($"どこどこテクスチャ: Selected object '{currentSelectedObject.name}' has no Renderer component.");
                return;
            }

            availableMaterials = rend.sharedMaterials;
            if (availableMaterials == null || availableMaterials.Length == 0)
            {
                if (enableDebugLog) Debug.LogWarning($"どこどこテクスチャ: Renderer on '{currentSelectedObject.name}' has no materials.");
                availableMaterials = null;
                return;
            }

            materialDisplayNames = new string[availableMaterials.Length];
            bool foundValidMaterial = false;
            for (int i = 0; i < availableMaterials.Length; i++)
            {
                materialDisplayNames[i] = (availableMaterials[i] != null) ? $"{i}: {availableMaterials[i].name}" : $"{i}: (None)";
                if (!foundValidMaterial && availableMaterials[i] != null)
                {
                    selectedMaterialIndex = i;
                    lastSelectedMaterialInstanceID = availableMaterials[i].GetInstanceID();
                    foundValidMaterial = true;
                }
            }
            if (!foundValidMaterial) {
                 if(enableDebugLog) Debug.LogWarning($"どこどこテクスチャ: No valid materials found on '{currentSelectedObject.name}'.");
                 selectedMaterialIndex = 0;
                 lastSelectedMaterialInstanceID = 0;
            }
        }
    }

private void OnGUI()
{
    if (enableDebugLog) Debug.Log($"どこどこテクスチャ: OnGUI - Event: {Event.current.type}, hasUV: {hasUV}, currentDisplayTexture is null: {currentDisplayTexture == null}, Selected: {Selection.activeGameObject?.name}");

    //GUILayout.Label("どこどこテクスチャ", EditorStyles.boldLabel);

    EditorGUI.BeginChangeCheck(); // --- 設定変更の監視開始 ---
    circleRadius = EditorGUILayout.Slider("マーカーの半径", circleRadius, 1f, 20f);
    markerColor = EditorGUILayout.ColorField("マーカーの色", markerColor);
    if (EditorGUI.EndChangeCheck()) // --- 設定変更があったか ---
    {
        SaveSettings(); // 変更があれば設定を保存
        if (enableDebugLog) Debug.Log("どこどこテクスチャ: Settings updated and saved due to GUI change.");
        Repaint(); // マーカーの色などが変わったので再描画
    }
    EditorGUILayout.Space();

    UpdateMaterialListIfNeeded(false);
    GameObject selectedObject = lastSelectedObject; // OnGUIの冒頭で宣言・初期化

    if (selectedObject == null)
    {
        GUILayout.Label("オブジェクトを選択してください");
        if (currentDisplayTexture != null) { currentDisplayTexture = null; hasUV = false; }
        return;
    }

    Renderer rend = selectedObject.GetComponent<Renderer>(); // OnGUIの冒頭で宣言・初期化
    if (rend == null) {
        GUILayout.Label("選択されたオブジェクトにRendererがありません");
        if (currentDisplayTexture != null) { currentDisplayTexture = null; hasUV = false; }
        return;
    }

    Texture2D textureFromSelectedMaterialAttempt = null;

    if (availableMaterials == null || availableMaterials.Length == 0)
    {
        GUILayout.Label("選択されたオブジェクトにマテリアルがありません");
        if (currentDisplayTexture != null) { currentDisplayTexture = null; hasUV = false; }
        // currentDisplayTexture が null になるので、この後の main if/else で else 側が実行される
    }
    else
    {
        bool materialSelectionUIChanged = false;
        if (availableMaterials.Length > 1)
        {
            int previousMaterialIndex = selectedMaterialIndex;
            if (selectedMaterialIndex < 0 || selectedMaterialIndex >= availableMaterials.Length) selectedMaterialIndex = 0;

            selectedMaterialIndex = EditorGUILayout.Popup("Material", selectedMaterialIndex, materialDisplayNames);
            if (previousMaterialIndex != selectedMaterialIndex)
            {
                materialSelectionUIChanged = true;
            }
            EditorGUILayout.Space();
        } else if (availableMaterials.Length == 1) { 
            if (selectedMaterialIndex != 0) { 
                selectedMaterialIndex = 0;
                materialSelectionUIChanged = true; 
            }
        }

        if (materialSelectionUIChanged) {
            if (enableDebugLog) Debug.Log($"どこどこテクスチャ: Material selection changed to index {selectedMaterialIndex} via UI.");
            currentDisplayTexture = null;
            hasUV = false;
            if (selectedMaterialIndex >= 0 && selectedMaterialIndex < availableMaterials.Length && availableMaterials[selectedMaterialIndex] != null) {
                lastSelectedMaterialInstanceID = availableMaterials[selectedMaterialIndex].GetInstanceID();
            } else {
                lastSelectedMaterialInstanceID = 0;
            }
        }

        if (selectedMaterialIndex < 0 || selectedMaterialIndex >= availableMaterials.Length) {
            GUILayout.Label("マテリアル選択が無効です");
            if (currentDisplayTexture != null) { currentDisplayTexture = null; hasUV = false; }
        } else {
            Material currentMaterialToInspect = availableMaterials[selectedMaterialIndex];
            if (currentMaterialToInspect == null)
            {
                GUILayout.Label("選択されたマテリアルスロットが空です (None)");
                if (currentDisplayTexture != null) { currentDisplayTexture = null; hasUV = false; }
            }
            else
            {
                if (currentMaterialToInspect.GetInstanceID() != lastSelectedMaterialInstanceID) {
                    if (enableDebugLog) Debug.Log($"どこどこテクスチャ: Inspected material instance ID changed (is {currentMaterialToInspect.GetInstanceID()}, was {lastSelectedMaterialInstanceID}). Forcing texture refresh.");
                    currentDisplayTexture = null;
                    hasUV = false;
                    lastSelectedMaterialInstanceID = currentMaterialToInspect.GetInstanceID();
                }

                if (currentDisplayTexture == null) 
                {
                    if (enableDebugLog) Debug.Log($"どこどこテクスチャ: Attempting to get texture from material '{currentMaterialToInspect.name}'");
                    string[] commonTexturePropertyNames = { "_MainTex", "_BaseMap", "_AlbedoTex", "_ColorTex", "_DiffuseTex" /* 他のシェーダー用プロパティ名も追加可能 */ };
                    foreach (string propName in commonTexturePropertyNames)
                    {
                        if (currentMaterialToInspect.HasProperty(propName))
                        {
                            Texture tex = currentMaterialToInspect.GetTexture(propName);
                            if (tex != null) {
                                if (tex is Texture2D) { textureFromSelectedMaterialAttempt = tex as Texture2D; if (enableDebugLog) Debug.Log($"どこどこテクスチャ: Successfully got Texture2D '{textureFromSelectedMaterialAttempt.name}' from property '{propName}'."); break; }
                                else { if (enableDebugLog) Debug.LogWarning($"どこどこテクスチャ: Texture from property '{propName}' ('{tex.name}') is not a Texture2D. Actual type: {tex.GetType().Name}."); }
                            } else { if (enableDebugLog) Debug.LogWarning($"どこどこテクスチャ: Material has property '{propName}', but GetTexture('{propName}') returned null."); }
                        } 
                        if (textureFromSelectedMaterialAttempt != null) break;
                    }
                    if (textureFromSelectedMaterialAttempt == null) {
                        Texture mainTexFallback = currentMaterialToInspect.mainTexture; 
                        if (mainTexFallback != null) {
                            if (mainTexFallback is Texture2D) { textureFromSelectedMaterialAttempt = mainTexFallback as Texture2D; if (enableDebugLog) Debug.Log($"どこどこテクスチャ: Successfully got Texture2D '{textureFromSelectedMaterialAttempt.name}' from material.mainTexture fallback for '{currentMaterialToInspect.name}'."); }
                            else { if (enableDebugLog) Debug.LogWarning($"どこどこテクスチャ: material.mainTexture ('{mainTexFallback.name}') for '{currentMaterialToInspect.name}' was not null, but it's not a Texture2D. Actual type: {mainTexFallback.GetType().Name}."); }
                        } else { if (enableDebugLog) Debug.LogWarning($"どこどこテクスチャ: material.mainTexture fallback for '{currentMaterialToInspect.name}' also returned null."); }
                    }
                    currentDisplayTexture = textureFromSelectedMaterialAttempt;
                    if (currentDisplayTexture == null) hasUV = false;
                    else Repaint(); 
                }
            }
        }
    } // End of: else for (availableMaterials == null || availableMaterials.Length == 0)

    // --- ここからがテクスチャ表示のメイン分岐 ---
    if (currentDisplayTexture != null)
    {
        GUILayout.Label("プレビュー", EditorStyles.boldLabel);
        GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();
        float textureAspect = 1.0f;
        try { if (currentDisplayTexture.width > 0 && currentDisplayTexture.height > 0) textureAspect = (float)currentDisplayTexture.width / currentDisplayTexture.height; }
        catch (UnityException ex) { if(enableDebugLog) Debug.LogError($"Error getting texture dimensions for '{currentDisplayTexture.name}': {ex.Message}"); textureAspect = 1.0f; /* エラー時デフォルト */ }
        Rect availableRect = GUILayoutUtility.GetRect(10, 10000, 10, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        float previewWidth = availableRect.width;
        float previewHeight = previewWidth / textureAspect;
        if (previewHeight > availableRect.height) { previewHeight = availableRect.height; previewWidth = previewHeight * textureAspect; }
        float xOffset = (availableRect.width - previewWidth) / 2;
        float yOffset = (availableRect.height - previewHeight) / 2;
        texturePreviewRect = new Rect(availableRect.x + xOffset, availableRect.y + yOffset, previewWidth, previewHeight);
        GUILayout.Label("", GUILayout.Height(texturePreviewRect.height), GUILayout.Width(texturePreviewRect.width));
        if (Event.current.type == EventType.Repaint) {
            GUI.DrawTexture(texturePreviewRect, currentDisplayTexture, ScaleMode.ScaleToFit);
            if (hasUV) DrawMarker(texturePreviewRect, uvCoord);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        EditorGUILayout.Space();

        if (GUILayout.Button("テクスチャの場所を開く"))
        {
            string path = AssetDatabase.GetAssetPath(currentDisplayTexture);
            if (!string.IsNullOrEmpty(path)) EditorUtility.RevealInFinder(path);
            else { ShowNotification(new GUIContent("Asset path not found (Runtime texture?).")); if (enableDebugLog) Debug.LogWarning("どこどこテクスチャ: Could not find asset path for current texture."); }
        }
        
        float helpMessageHeight = EditorGUIUtility.singleLineHeight; 

        if (!hasUV) 
        {
            GUILayout.Label("モデルにカーソルを合わせてください", GUILayout.Height(helpMessageHeight));
        }
        else
        {
            GUILayout.Space(helpMessageHeight);
        }
    } // --- ここで if (currentDisplayTexture != null) のブロックが終わる ---
    else // --- これは if (currentDisplayTexture != null) の else 節 ---
    {
        // selectedObject と rend は OnGUI の冒頭で取得済みなので、ここではそのまま使える
        if (selectedObject != null) { // selectedObject が null でないことの確認は残す
            if (rend != null && (availableMaterials == null || availableMaterials.Length == 0) ) GUILayout.Label("選択されたオブジェクトにマテリアルがありません");
            else if (availableMaterials != null && availableMaterials.Length > 0 && (selectedMaterialIndex <0 || selectedMaterialIndex >= availableMaterials.Length || (selectedMaterialIndex < availableMaterials.Length && availableMaterials[selectedMaterialIndex] == null)) ) GUILayout.Label("選択されたマテリアルスロットが空か、選択が無効です");
            else GUILayout.Label("表示可能なTextureが見つかりません");
        }
        // else の場合、基本的には selectedObject が null でないことは保証されているが、念のため
        // currentDisplayTexture が null なら、hasUV は false にする
        if (hasUV) hasUV = false;
    }
} // --- OnGUI メソッドの終わり ---

    private void DrawMarker(Rect previewArea, Vector2 currentUV)
    {
        if (currentDisplayTexture == null) return; // テクスチャがない場合は描画しない

        Handles.BeginGUI(); // OnGUI外なので必須
        float markerX = previewArea.x + currentUV.x * previewArea.width;
        float markerY = previewArea.y + (1.0f - currentUV.y) * previewArea.height;
        Vector2 markerCenter = new Vector2(markerX, markerY);
        Handles.color = markerColor; // 保存された色を使用
        Handles.DrawSolidDisc(markerCenter, Vector3.forward, circleRadius);
        Handles.EndGUI();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (currentDisplayTexture == null || lastSelectedObject == null)
        {
            if (hasUV) { hasUV = false; Repaint(); }
            if(tempSmrColliderHost != null && tempSmrColliderHost.activeSelf) tempSmrColliderHost.SetActive(false);
            return;
        }

        Renderer selectedRenderer = lastSelectedObject.GetComponent<Renderer>();
        if (selectedRenderer == null) {
            if (hasUV) { hasUV = false; Repaint(); }
            if(tempSmrColliderHost != null && tempSmrColliderHost.activeSelf) tempSmrColliderHost.SetActive(false);
            return;
        }

        if (!(e.type == EventType.MouseMove || e.type == EventType.MouseDrag))
        {
            if (e.type != EventType.Layout && e.type != EventType.Repaint) {
                // SMRコライダーが不要なイベントタイプなら非アクティブ化を試みる
                if(tempSmrColliderHost != null && tempSmrColliderHost.activeSelf && !(selectedRenderer is SkinnedMeshRenderer)) {
                     // SMRでない場合はマウス操作時以外は不要なことが多い
                    tempSmrColliderHost.SetActive(false);
                }
                return;
            }
            // Layout, Repaint時はマーカー再描画のために通すがRaycastはしない
            // if (hasUV) Repaint(); // 必要に応じて
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        bool hitFoundThisFrame = false;
        Vector2 newUV = Vector2.zero;
        LayerMask layerMask = (1 << selectedRenderer.gameObject.layer); // 選択オブジェクトのレイヤーのみ対象 (より堅牢な設定も可能)
        if (selectedRenderer.gameObject.layer == 0) layerMask = Physics.DefaultRaycastLayers; // Defaultレイヤーは特別扱い


        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
            if (hitRenderer != null && hitRenderer == selectedRenderer)
            {
                newUV = hit.textureCoord;
                hitFoundThisFrame = true;
            }
        }

        if (!hitFoundThisFrame && selectedRenderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
        {
            if (tempSmrColliderHost == null || tempSmrCollider == null || bakedSmrMesh == null) {
                InitializeSceneGuiResources();
                if (tempSmrColliderHost == null || tempSmrCollider == null || bakedSmrMesh == null) {
                    if(enableDebugLog) Debug.LogError("どこどこテクスチャ: SMR resources failed to initialize in OnSceneGUI.");
                    return;
                }
            }

            try
            {
                if(!tempSmrColliderHost.activeSelf) tempSmrColliderHost.SetActive(true);
                // bakedSmrMesh.Clear(); // アニメーションする場合は毎フレームクリア＆ベイクが必要
                smr.BakeMesh(bakedSmrMesh, true);

                if (bakedSmrMesh.vertexCount > 0)
                {
                    tempSmrColliderHost.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                    tempSmrColliderHost.transform.localScale = smr.transform.lossyScale;
                    tempSmrCollider.sharedMesh = null;
                    tempSmrCollider.sharedMesh = bakedSmrMesh;

                    if (tempSmrCollider.Raycast(ray, out RaycastHit bakedHit, Mathf.Infinity))
                    {
                        newUV = bakedHit.textureCoord;
                        hitFoundThisFrame = true;
                    }
                }
            }
            catch (System.Exception ex) { if (enableDebugLog) Debug.LogError($"どこどこテクスチャ: OnSceneGUI - Error during SMR processing: {ex.Message}"); }
        } else if (tempSmrColliderHost != null && tempSmrColliderHost.activeSelf && !(selectedRenderer is SkinnedMeshRenderer)) {
            // SMRでない、またはSMR処理が不要だった場合は非アクティブ化
            tempSmrColliderHost.SetActive(false);
        }


        bool needsRepaint = false;
        if (hitFoundThisFrame)
        {
            if (!hasUV || uvCoord != newUV) { uvCoord = newUV; hasUV = true; needsRepaint = true; }
        }
        else
        {
            if (hasUV) { hasUV = false; needsRepaint = true; }
        }

        if (needsRepaint) Repaint();
    }
}
// --- END OF FILE どこどこテクスチャWindow.cs ---