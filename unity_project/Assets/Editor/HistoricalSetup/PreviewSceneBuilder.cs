using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using A3R.ARGuidance;

namespace A3R.EditorTools
{
    public static class PreviewSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/A3R_AR_Guidance_Preview.unity";

        [MenuItem("A3R/Build AR Guidance Preview Scene")]
        public static void BuildPreviewScene()
        {
            Debug.Log("[PreviewSceneBuilder] Building Calibrated AR Guidance & Physical Scene: " + ScenePath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera & Refined Tabletop Studio Lighting Setup
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            cam.fieldOfView = 45f;
            // Positioned for natural tabletop eye-level inspection without perspective distortion
            camObj.transform.position = new Vector3(0.10f, 0.22f, -0.16f);
            camObj.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            camObj.AddComponent<AudioListener>();

            // Key Light (Warm soft directional light)
            GameObject lightObj = new GameObject("Directional Light (Key)");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.98f, 0.95f);
            light.intensity = 1.20f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // Soft Fill Light (Cool ambient balance)
            GameObject fillLightObj = new GameObject("Directional Light (Fill)");
            Light fillLight = fillLightObj.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.55f, 0.65f, 0.85f);
            fillLight.intensity = 0.50f;
            fillLight.shadows = LightShadows.None;
            fillLightObj.transform.rotation = Quaternion.Euler(-20f, 140f, 0f);

            // Ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.24f, 0.30f);

            // 2. EventSystem with InputSystemUIInputModule
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var inputModule = eventSystemObj.AddComponent<InputSystemUIInputModule>();
            var defaultActions = Resources.Load<InputActionAsset>("DefaultInputActions") ??
                                 AssetDatabase.LoadAssetAtPath<InputActionAsset>("Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions");
            if (defaultActions != null)
            {
                inputModule.actionsAsset = defaultActions;
            }

            // 3. Tabletop Content Root
            GameObject tabletopRoot = new GameObject("A3R_TabletopContentRoot");
            tabletopRoot.transform.position = Vector3.zero;
            tabletopRoot.transform.rotation = Quaternion.identity;

            // Workbench Table Mat (matte charcoal antistatic electronics mat)
            GameObject tableSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tableSurface.name = "TableSurface";
            tableSurface.transform.SetParent(tabletopRoot.transform, false);
            tableSurface.transform.localPosition = new Vector3(0.10f, -0.005f, 0.035f);
            tableSurface.transform.localScale = new Vector3(0.72f, 0.01f, 0.52f);
            Material tableMat = ProceduralMeshUtility.CreateMaterial(new Color(0.14f, 0.16f, 0.19f), false, false);
            tableSurface.GetComponent<MeshRenderer>().material = tableMat;

            // Marker card representation
            GameObject markerCard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            markerCard.name = "A3R_MarkerCard";
            markerCard.transform.SetParent(tabletopRoot.transform, false);
            markerCard.transform.localPosition = new Vector3(0.00f, 0.001f, -0.04f);
            markerCard.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            markerCard.transform.localScale = new Vector3(0.08f, 0.08f, 1f);
            Material markerMat = ProceduralMeshUtility.CreateMaterial(new Color(0.92f, 0.92f, 0.94f), false, false);
            markerCard.GetComponent<MeshRenderer>().material = markerMat;

            // -------------------------------------------------------------
            // REALISTIC HALF-SIZE BREADBOARD MODEL
            // -------------------------------------------------------------
            GameObject breadboardObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            breadboardObj.name = "Breadboard";
            breadboardObj.transform.SetParent(tabletopRoot.transform, false);
            breadboardObj.transform.localPosition = new Vector3(0.155f, 0.005f, 0.035f);
            breadboardObj.transform.localScale = new Vector3(0.165f, 0.009f, 0.055f);
            Material bbMat = ProceduralMeshUtility.CreateMaterial(new Color(0.92f, 0.92f, 0.89f), false, false);
            Renderer bbRenderer = breadboardObj.GetComponent<MeshRenderer>();
            bbRenderer.material = bbMat;

            // Central divider trench running lengthwise
            GameObject bbCenterGap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bbCenterGap.name = "CenterTrench";
            bbCenterGap.transform.SetParent(breadboardObj.transform, false);
            bbCenterGap.transform.localPosition = new Vector3(0f, 0.51f, 0f);
            bbCenterGap.transform.localScale = new Vector3(1.0f, 0.06f, 0.05f);
            Material gapMat = ProceduralMeshUtility.CreateMaterial(new Color(0.68f, 0.68f, 0.66f), false, false);
            bbCenterGap.GetComponent<MeshRenderer>().material = gapMat;

            // Power Rail Stripes (+ Red, - Blue)
            CreateRailStripe(breadboardObj.transform, "RedRailTop", new Vector3(0f, 0.52f, 0.44f), new Color(0.85f, 0.15f, 0.15f));
            CreateRailStripe(breadboardObj.transform, "BlueRailTop", new Vector3(0f, 0.52f, 0.38f), new Color(0.15f, 0.35f, 0.85f));
            CreateRailStripe(breadboardObj.transform, "BlueRailBot", new Vector3(0f, 0.52f, -0.38f), new Color(0.15f, 0.35f, 0.85f));
            CreateRailStripe(breadboardObj.transform, "RedRailBot", new Vector3(0f, 0.52f, -0.44f), new Color(0.85f, 0.15f, 0.15f));

            // Pin-hole matrix representation (terminal socket rows)
            CreateTerminalSocketRows(breadboardObj.transform);

            // -------------------------------------------------------------
            // RECOGNIZABLE ESP32 DEVELOPMENT BOARD MODEL
            // -------------------------------------------------------------
            GameObject esp32Obj = new GameObject("ESP32_Board_Placeholder");
            esp32Obj.transform.SetParent(tabletopRoot.transform, false);
            esp32Obj.transform.localPosition = new Vector3(0.045f, 0.005f, 0.035f);

            // Dark PCB Base
            GameObject esp32Pcb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            esp32Pcb.name = "ESP32_PCB";
            esp32Pcb.transform.SetParent(esp32Obj.transform, false);
            esp32Pcb.transform.localPosition = Vector3.zero;
            esp32Pcb.transform.localScale = new Vector3(0.052f, 0.0024f, 0.028f);
            Material pcbMat = ProceduralMeshUtility.CreateMaterial(new Color(0.10f, 0.12f, 0.14f), false, false);
            Renderer esp32Renderer = esp32Pcb.GetComponent<MeshRenderer>();
            esp32Renderer.material = pcbMat;

            // 4 Corner Mounting Holes
            CreateMountingHole(esp32Obj.transform, "Hole_TL", new Vector3(-0.022f, 0.0013f, 0.011f));
            CreateMountingHole(esp32Obj.transform, "Hole_TR", new Vector3(0.022f, 0.0013f, 0.011f));
            CreateMountingHole(esp32Obj.transform, "Hole_BL", new Vector3(-0.022f, 0.0013f, -0.011f));
            CreateMountingHole(esp32Obj.transform, "Hole_BR", new Vector3(0.022f, 0.0013f, -0.011f));

            // Metal RF Shield Box (ESP32-WROOM-32)
            GameObject esp32Shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
            esp32Shield.name = "ESP32_Shield";
            esp32Shield.transform.SetParent(esp32Obj.transform, false);
            esp32Shield.transform.localPosition = new Vector3(0.008f, 0.0026f, 0f);
            esp32Shield.transform.localScale = new Vector3(0.018f, 0.0035f, 0.016f);
            Material shieldMat = ProceduralMeshUtility.CreateMaterial(new Color(0.82f, 0.85f, 0.88f), false, false);
            esp32Shield.GetComponent<MeshRenderer>().material = shieldMat;

            // Dual 15-pin Black Header Strips along both edges
            GameObject leftHeaders = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftHeaders.name = "Headers_Left";
            leftHeaders.transform.SetParent(esp32Obj.transform, false);
            leftHeaders.transform.localPosition = new Vector3(0f, 0.0035f, 0.011f);
            leftHeaders.transform.localScale = new Vector3(0.042f, 0.005f, 0.0025f);
            Material headerMat = ProceduralMeshUtility.CreateMaterial(new Color(0.08f, 0.08f, 0.08f), false, false);
            leftHeaders.GetComponent<MeshRenderer>().material = headerMat;

            GameObject rightHeaders = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightHeaders.name = "Headers_Right";
            rightHeaders.transform.SetParent(esp32Obj.transform, false);
            rightHeaders.transform.localPosition = new Vector3(0f, 0.0035f, -0.011f);
            rightHeaders.transform.localScale = new Vector3(0.042f, 0.005f, 0.0025f);
            rightHeaders.GetComponent<MeshRenderer>().material = headerMat;

            // Micro-USB Connector Housing
            GameObject usbPort = GameObject.CreatePrimitive(PrimitiveType.Cube);
            usbPort.name = "USB_Port";
            usbPort.transform.SetParent(esp32Obj.transform, false);
            usbPort.transform.localPosition = new Vector3(-0.025f, 0.0022f, 0f);
            usbPort.transform.localScale = new Vector3(0.0075f, 0.0030f, 0.0075f);
            Material usbMat = ProceduralMeshUtility.CreateMaterial(new Color(0.75f, 0.75f, 0.78f), false, false);
            usbPort.GetComponent<MeshRenderer>().material = usbMat;

            // Tactile EN and BOOT Buttons
            CreateTactileButton(esp32Obj.transform, "Btn_EN", new Vector3(-0.018f, 0.0020f, 0.008f));
            CreateTactileButton(esp32Obj.transform, "Btn_BOOT", new Vector3(-0.018f, 0.0020f, -0.008f));

            // Floating Disclaimer Label above ESP32 Board
            GameObject espDisclaimer = new GameObject("ESP32_DisclaimerLabel");
            espDisclaimer.transform.SetParent(esp32Obj.transform, false);
            espDisclaimer.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            espDisclaimer.transform.localScale = Vector3.one * 0.0006f;
            GameObject dscBg = new GameObject("Bg");
            dscBg.transform.SetParent(espDisclaimer.transform, false);
            var dscRt = dscBg.AddComponent<RectTransform>();
            dscRt.sizeDelta = new Vector2(240, 60);
            var dscImg = dscBg.AddComponent<Image>();
            dscImg.color = new Color(0.04f, 0.07f, 0.12f, 0.85f);
            var dscTxt = CreateUIText(dscBg.transform, "Txt", "ESP32 BOARD\nVISUAL PLACEHOLDER", 11, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.85f, 0.90f, 0.95f));

            // -------------------------------------------------------------
            // COHERENT ELECTRONIC CIRCUIT COMPONENTS
            // -------------------------------------------------------------

            // 220 Ohm Resistor (Step 2)
            GameObject resistorObj = new GameObject("Resistor_220Ohm");
            resistorObj.transform.SetParent(tabletopRoot.transform, false);
            resistorObj.transform.localPosition = new Vector3(0.142f, 0.011f, 0.042f);

            GameObject resBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            resBody.name = "ResistorBody";
            resBody.transform.SetParent(resistorObj.transform, false);
            resBody.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            resBody.transform.localScale = new Vector3(0.003f, 0.007f, 0.003f);
            Material resMat = ProceduralMeshUtility.CreateMaterial(new Color(0.88f, 0.80f, 0.65f), false, false);
            Renderer resRenderer = resBody.GetComponent<MeshRenderer>();
            resRenderer.material = resMat;

            // Color bands: Red, Red, Brown, Gold
            CreateColorBand(resBody.transform, "Band1_Red", -0.4f, new Color(0.85f, 0.1f, 0.1f));
            CreateColorBand(resBody.transform, "Band2_Red", -0.15f, new Color(0.85f, 0.1f, 0.1f));
            CreateColorBand(resBody.transform, "Band3_Brown", 0.1f, new Color(0.45f, 0.25f, 0.1f));
            CreateColorBand(resBody.transform, "Band4_Gold", 0.4f, new Color(0.85f, 0.70f, 0.2f));
            resistorObj.SetActive(false);

            // 5mm Red LED (Step 3 & Step 8)
            GameObject ledObj = new GameObject("LED_Red");
            ledObj.transform.SetParent(tabletopRoot.transform, false);
            ledObj.transform.localPosition = new Vector3(0.155f, 0.014f, 0.042f);

            GameObject ledDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ledDome.name = "LED_Dome";
            ledDome.transform.SetParent(ledObj.transform, false);
            ledDome.transform.localScale = new Vector3(0.005f, 0.007f, 0.005f);
            Material ledMat = ProceduralMeshUtility.CreateMaterial(new Color(1.0f, 0.15f, 0.15f, 0.90f), true, true);
            Renderer ledRenderer = ledDome.GetComponent<MeshRenderer>();
            ledRenderer.material = ledMat;

            // LED wire legs
            CreateWireLead(ledObj.transform, "AnodeLeg", new Vector3(-0.0015f, -0.004f, 0f), 0.006f);
            CreateWireLead(ledObj.transform, "CathodeLeg", new Vector3(0.0015f, -0.004f, 0f), 0.005f);
            ledObj.SetActive(false);

            // Wire Pin 13 (Step 4) — Yellow jumper connecting ESP32 GPIO 13 to Row 10
            GameObject wirePin13Obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wirePin13Obj.name = "Wire_Pin13";
            wirePin13Obj.transform.SetParent(tabletopRoot.transform, false);
            wirePin13Obj.transform.localPosition = new Vector3(0.099f, 0.013f, 0.042f);
            wirePin13Obj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wirePin13Obj.transform.localScale = new Vector3(0.0018f, 0.043f, 0.0018f);
            Material wireYellowMat = ProceduralMeshUtility.CreateMaterial(new Color(1.0f, 0.85f, 0.10f), false, false);
            Renderer wirePin13Renderer = wirePin13Obj.GetComponent<MeshRenderer>();
            wirePin13Renderer.material = wireYellowMat;
            wirePin13Obj.SetActive(false);

            // Wire GND (Step 5) — Black jumper connecting ESP32 GND to Row 15
            GameObject wireGndObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wireGndObj.name = "Wire_GND";
            wireGndObj.transform.SetParent(tabletopRoot.transform, false);
            wireGndObj.transform.localPosition = new Vector3(0.096f, 0.013f, 0.018f);
            wireGndObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wireGndObj.transform.localScale = new Vector3(0.0018f, 0.062f, 0.0018f);
            Material wireBlackMat = ProceduralMeshUtility.CreateMaterial(new Color(0.12f, 0.12f, 0.14f), false, false);
            Renderer wireGndRenderer = wireGndObj.GetComponent<MeshRenderer>();
            wireGndRenderer.material = wireBlackMat;
            wireGndObj.SetActive(false);

            // USB Cable (Step 6)
            GameObject usbCableObj = new GameObject("USB_Cable");
            usbCableObj.transform.SetParent(tabletopRoot.transform, false);
            usbCableObj.transform.localPosition = new Vector3(0.018f, 0.008f, 0.035f);

            GameObject usbMoldedPlug = GameObject.CreatePrimitive(PrimitiveType.Cube);
            usbMoldedPlug.name = "USB_Plug";
            usbMoldedPlug.transform.SetParent(usbCableObj.transform, false);
            usbMoldedPlug.transform.localPosition = new Vector3(-0.010f, 0f, 0f);
            usbMoldedPlug.transform.localScale = new Vector3(0.018f, 0.006f, 0.010f);
            Material usbCableMat = ProceduralMeshUtility.CreateMaterial(new Color(0.18f, 0.20f, 0.22f), false, false);
            Renderer usbCableRenderer = usbMoldedPlug.GetComponent<MeshRenderer>();
            usbCableRenderer.material = usbCableMat;

            GameObject usbCord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            usbCord.name = "USB_Cord";
            usbCord.transform.SetParent(usbCableObj.transform, false);
            usbCord.transform.localPosition = new Vector3(-0.045f, -0.001f, 0f);
            usbCord.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            usbCord.transform.localScale = new Vector3(0.0035f, 0.025f, 0.0035f);
            usbCord.GetComponent<MeshRenderer>().material = usbCableMat;
            usbCableObj.SetActive(false);

            // -------------------------------------------------------------
            // 4. PRECISION TARGET ANCHORS
            // -------------------------------------------------------------
            GameObject anchorsRoot = new GameObject("TargetAnchors");
            anchorsRoot.transform.SetParent(tabletopRoot.transform, false);

            CreateAnchor(anchorsRoot.transform, "breadboard_center", new Vector3(0.155f, 0.010f, 0.035f));
            CreateAnchor(anchorsRoot.transform, "resistor_slot_row10_colE", new Vector3(0.142f, 0.010f, 0.042f));
            CreateAnchor(anchorsRoot.transform, "led_slot_row14_colD", new Vector3(0.155f, 0.010f, 0.042f));
            CreateAnchor(anchorsRoot.transform, "board_gpio13", new Vector3(0.056f, 0.011f, 0.042f));
            CreateAnchor(anchorsRoot.transform, "board_gnd", new Vector3(0.034f, 0.011f, 0.018f));
            CreateAnchor(anchorsRoot.transform, "board_usb_port", new Vector3(0.018f, 0.008f, 0.035f));
            CreateAnchor(anchorsRoot.transform, "led_position", new Vector3(0.155f, 0.020f, 0.042f));

            // Legacy alias anchors for backward compatibility
            CreateAnchor(anchorsRoot.transform, "Anchor_Step1_Breadboard", new Vector3(0.155f, 0.010f, 0.035f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Step2_ESP32", new Vector3(0.045f, 0.008f, 0.035f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Step3_Resistor", new Vector3(0.142f, 0.010f, 0.042f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Step4_LED", new Vector3(0.155f, 0.010f, 0.042f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Step5_Wire13", new Vector3(0.056f, 0.011f, 0.042f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Step6_WireGND", new Vector3(0.034f, 0.011f, 0.018f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Step7_IDE", new Vector3(0.155f, 0.12f, 0.05f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Step8_USB", new Vector3(0.018f, 0.008f, 0.035f));

            // -------------------------------------------------------------
            // 5. VISUAL GUIDANCE OVERLAYS & ACCESSORIES
            // -------------------------------------------------------------
            GameObject targetHighlightObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetHighlightObj.name = "A3R_TargetHighlight";
            targetHighlightObj.transform.SetParent(tabletopRoot.transform, false);
            targetHighlightObj.transform.localScale = new Vector3(0.05f, 0.002f, 0.05f);
            Material hlMat = ProceduralMeshUtility.CreateMaterial(new Color(0.0f, 0.90f, 1.0f, 0.35f), true, true);
            targetHighlightObj.GetComponent<MeshRenderer>().material = hlMat;
            targetHighlightObj.SetActive(false);

            // World-space Floating Target Label (compact, billboarded)
            GameObject floatingLabelObj = new GameObject("TargetFloatingLabel");
            floatingLabelObj.transform.SetParent(tabletopRoot.transform, false);
            floatingLabelObj.transform.localScale = Vector3.one * 0.0008f;

            GameObject flBg = new GameObject("Bg");
            flBg.transform.SetParent(floatingLabelObj.transform, false);
            RectTransform flBgRt = flBg.AddComponent<RectTransform>();
            flBgRt.sizeDelta = new Vector2(190, 56);
            Image flBgImg = flBg.AddComponent<Image>();
            flBgImg.color = new Color(0.04f, 0.07f, 0.12f, 0.88f);

            GameObject flAccent = new GameObject("Accent");
            flAccent.transform.SetParent(flBg.transform, false);
            RectTransform flAccentRt = flAccent.AddComponent<RectTransform>();
            flAccentRt.anchorMin = new Vector2(0, 0);
            flAccentRt.anchorMax = new Vector2(0, 1);
            flAccentRt.sizeDelta = new Vector2(4, 0);
            flAccentRt.anchoredPosition = new Vector2(2f, 0);
            Image flAccentImg = flAccent.AddComponent<Image>();
            flAccentImg.color = new Color(0.0f, 0.90f, 1.0f, 1.0f);

            GameObject flStep = new GameObject("StepText");
            flStep.transform.SetParent(flBg.transform, false);
            TMP_Text flStepTmp = flStep.AddComponent<TextMeshProUGUI>();
            flStepTmp.fontSize = 13;
            flStepTmp.fontStyle = FontStyles.Bold;
            flStepTmp.color = new Color(0.0f, 0.90f, 1.0f);
            flStepTmp.text = "STEP 1";
            flStepTmp.rectTransform.anchoredPosition = new Vector2(12, 10);
            flStepTmp.rectTransform.sizeDelta = new Vector2(160, 20);

            GameObject flTitle = new GameObject("TitleText");
            flTitle.transform.SetParent(flBg.transform, false);
            TMP_Text flTitleTmp = flTitle.AddComponent<TextMeshProUGUI>();
            flTitleTmp.fontSize = 11;
            flTitleTmp.color = Color.white;
            flTitleTmp.text = "Place Breadboard";
            flTitleTmp.rectTransform.anchoredPosition = new Vector2(12, -10);
            flTitleTmp.rectTransform.sizeDelta = new Vector2(160, 20);
            floatingLabelObj.SetActive(false);

            // Step 7 Arduino IDE Card (Off-marker IDE step)
            GameObject ideCardObj = new GameObject("A3R_Step7_IDECard");
            ideCardObj.transform.SetParent(tabletopRoot.transform, false);
            ideCardObj.transform.localPosition = new Vector3(0.12f, 0.10f, 0.04f);
            ideCardObj.transform.localScale = Vector3.one * 0.0008f;

            GameObject ideBg = new GameObject("Bg");
            ideBg.transform.SetParent(ideCardObj.transform, false);
            RectTransform ideBgRt = ideBg.AddComponent<RectTransform>();
            ideBgRt.sizeDelta = new Vector2(360, 200);
            Image ideBgImg = ideBg.AddComponent<Image>();
            ideBgImg.color = new Color(0.02f, 0.05f, 0.10f, 0.94f);

            CreateUIText(ideBg.transform, "IdeHeader", "STEP 7: UPLOAD BLINK SKETCH — OFF-MARKER IDE STEP", 13, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -12), new Vector2(-30, 22), new Color(0.0f, 0.90f, 1.0f));
            CreateUIText(ideBg.transform, "IdeCode", "void setup() { pinMode(13, OUTPUT); }\nvoid loop() { digitalWrite(13, HIGH);\n              delay(1000);\n              digitalWrite(13, LOW); delay(1000); }", 11, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(15, 30), new Vector2(-30, -50), new Color(0.85f, 0.90f, 0.95f));
            CreateUIText(ideBg.transform, "IdeStatus", "[OK] Ready to Upload -- Directional Path & Reticle Suppressed", 11, FontStyles.Italic, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(15, 8), new Vector2(-30, 20), new Color(0.2f, 0.9f, 0.4f));
            ideCardObj.SetActive(false);

            // 6. AR Guidance Components — Modern Chevron Path System
            GameObject guidanceMgrObj = new GameObject("ARGuidanceManager");
            ARGuidanceController guidanceCtrl = guidanceMgrObj.AddComponent<ARGuidanceController>();

            // Directional Arrow hosting Modern Chevron Path
            GameObject arrowObj = new GameObject("A3R_DirectionalArrow");
            arrowObj.transform.SetParent(guidanceMgrObj.transform, false);
            A3R_DirectionalArrow arrow = arrowObj.AddComponent<A3R_DirectionalArrow>();

            // Child ModernChevronPath component
            GameObject pathObj = new GameObject("ModernChevronPath");
            pathObj.transform.SetParent(arrowObj.transform, false);
            A3R_ChevronPath chevronPath = pathObj.AddComponent<A3R_ChevronPath>();
            chevronPath.InitializeChevrons();

            SerializedObject soArrow = new SerializedObject(arrow);
            soArrow.FindProperty("chevronPath").objectReferenceValue = chevronPath;
            soArrow.ApplyModifiedPropertiesWithoutUndo();

            // Target Ring hosting Modern Segmented Reticle & Holographic Beacon
            GameObject ringObj = new GameObject("A3R_TargetRing");
            ringObj.transform.SetParent(guidanceMgrObj.transform, false);
            A3R_TargetRing ring = ringObj.AddComponent<A3R_TargetRing>();
            ring.InitializeVisuals();

            GameObject highlightObj = new GameObject("A3R_HighlightAnchor");
            highlightObj.transform.SetParent(guidanceMgrObj.transform, false);

            SerializedObject soGuidance = new SerializedObject(guidanceCtrl);
            soGuidance.FindProperty("directionalArrow").objectReferenceValue = arrow;
            soGuidance.FindProperty("targetRing").objectReferenceValue = ring;
            soGuidance.FindProperty("highlightAnchor").objectReferenceValue = highlightObj;
            soGuidance.FindProperty("contentRootTransform").objectReferenceValue = tabletopRoot.transform;
            soGuidance.ApplyModifiedPropertiesWithoutUndo();

            // 7. Preview Controller
            A3R_ARGuidancePreviewController previewCtrl = guidanceMgrObj.AddComponent<A3R_ARGuidancePreviewController>();
            SerializedObject soPreview = new SerializedObject(previewCtrl);
            soPreview.FindProperty("guidanceController").objectReferenceValue = guidanceCtrl;
            soPreview.FindProperty("tabletopRoot").objectReferenceValue = tabletopRoot.transform;
            soPreview.FindProperty("targetHighlightAnchor").objectReferenceValue = targetHighlightObj;
            soPreview.FindProperty("targetFloatingLabel").objectReferenceValue = floatingLabelObj;
            soPreview.FindProperty("floatingLabelStepText").objectReferenceValue = flStepTmp;
            soPreview.FindProperty("floatingLabelTitleText").objectReferenceValue = flTitleTmp;
            soPreview.FindProperty("floatingLabelAccent").objectReferenceValue = flAccentImg;
            soPreview.FindProperty("step7IdeCard").objectReferenceValue = ideCardObj;

            soPreview.FindProperty("breadboardRenderer").objectReferenceValue = bbRenderer;
            soPreview.FindProperty("esp32BoardRenderer").objectReferenceValue = esp32Renderer;
            soPreview.FindProperty("resistorRenderer").objectReferenceValue = resRenderer;
            soPreview.FindProperty("ledRenderer").objectReferenceValue = ledRenderer;
            soPreview.FindProperty("wirePin13Renderer").objectReferenceValue = wirePin13Renderer;
            soPreview.FindProperty("wireGndRenderer").objectReferenceValue = wireGndRenderer;
            soPreview.FindProperty("usbCableRenderer").objectReferenceValue = usbCableRenderer;

            // 8. Canvas & Polished Modern UI HUD
            GameObject canvasObj = new GameObject("Canvas_PreviewHUD");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Top Left: Branding
            GameObject brandObj = CreateUIPanel(canvasObj.transform, "BrandPanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(25, -25), new Vector2(240, 65), new Color(0.04f, 0.07f, 0.12f, 0.85f));
            brandObj.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
            CreateUIText(brandObj.transform, "BrandTitle", "A3R -- Affect-Adaptive AR", 16, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -12), new Vector2(-20, 24), new Color(0.0f, 0.90f, 1.0f));
            CreateUIText(brandObj.transform, "BrandSub", "Offline AR Guidance Visual Preview", 11, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -36), new Vector2(-20, 20), new Color(0.70f, 0.75f, 0.85f));

            // Top Center: Prominent Step Banner & Title
            GameObject stepBannerObj = CreateUIPanel(canvasObj.transform, "StepBanner", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -25), new Vector2(560, 100), new Color(0.04f, 0.07f, 0.12f, 0.88f));
            stepBannerObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);

            TMP_Text topStepBannerTxt = CreateUIText(stepBannerObj.transform, "BannerText", "STEP 1 / 8", 18, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -12), new Vector2(0, 28), new Color(0.0f, 0.90f, 1.0f));
            TMP_Text topStepTitleTxt = CreateUIText(stepBannerObj.transform, "TitleText", "Place Breadboard", 16, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -42), new Vector2(0, 24), Color.white);
            TMP_Text stepInstructionTxt = CreateUIText(stepBannerObj.transform, "InstructionText", "Place the breadboard flat on the workbench beside the reference marker card.", 12, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -68), new Vector2(-30, 26), new Color(0.80f, 0.85f, 0.92f));

            // Top Right: Diagnostic HUD
            GameObject rightHudObj = CreateUIPanel(canvasObj.transform, "DiagnosticHUD", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-25, -25), new Vector2(340, 210), new Color(0.04f, 0.07f, 0.12f, 0.88f));
            rightHudObj.GetComponent<RectTransform>().pivot = new Vector2(1, 1);

            CreateUIText(rightHudObj.transform, "HUD_Header", "DIAGNOSTIC HUD", 13, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -12), new Vector2(-20, 20), new Color(0.0f, 0.90f, 1.0f));
            TMP_Text trackingStatusTxt = CreateUIText(rightHudObj.transform, "Txt_Tracking", "Tracking: TRACKING (Active)", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -34), new Vector2(-20, 20), Color.white);
            TMP_Text visualStateTxt = CreateUIText(rightHudObj.transform, "Txt_VisualState", "Visual State: Normal", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -54), new Vector2(-20, 20), Color.white);
            TMP_Text adaptationTxt = CreateUIText(rightHudObj.transform, "Txt_Adaptation", "Adaptation: Normal (Baseline)", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -74), new Vector2(-20, 20), Color.white);
            TMP_Text targetInfoTxt = CreateUIText(rightHudObj.transform, "Txt_Target", "Target: breadboard_center", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -94), new Vector2(-20, 20), Color.white);
            TMP_Text diagnosticsSummaryTxt = CreateUIText(rightHudObj.transform, "Txt_Summary", "Path: ON  |  Reticle: ON", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -114), new Vector2(-20, 20), Color.white);
            TMP_Text provisionalNoteTxt = CreateUIText(rightHudObj.transform, "Txt_Note", "Provisional software-test GPIO assignment: GPIO 13/12.", 10, FontStyles.Italic, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -140), new Vector2(-25, 60), new Color(0.85f, 0.85f, 0.65f));

            // Dev Mode Calibration Debug Telemetry Panel (Hidden by default)
            GameObject debugPanelObj = CreateUIPanel(canvasObj.transform, "CalibrationDebugPanel", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-25, -60), new Vector2(340, 240), new Color(0.02f, 0.04f, 0.08f, 0.94f));
            debugPanelObj.GetComponent<RectTransform>().pivot = new Vector2(1, 0.5f);
            TMP_Text debugTmp = CreateUIText(debugPanelObj.transform, "DebugText", "Calibration Telemetry Loading...", 11, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(15, 12), new Vector2(-30, -24), new Color(0.2f, 0.95f, 0.45f));
            debugPanelObj.SetActive(false);

            // Left Panel: Compact Control Buttons
            GameObject leftPanelObj = CreateUIPanel(canvasObj.transform, "ControlPanel", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(25, -20), new Vector2(220, 490), new Color(0.04f, 0.07f, 0.12f, 0.90f));
            leftPanelObj.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f);

            CreateUIText(leftPanelObj.transform, "CtrlTitle", "PREVIEW CONTROLS", 13, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -12), new Vector2(0, 22), new Color(0.0f, 0.90f, 1.0f));

            float btnY = -38f;
            float btnH = 32f;
            float btnGap = 6f;

            Button btnStepPrev = CreateUIButton(leftPanelObj.transform, "Btn_StepPrev", "< Prev Step", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnStepNext = CreateUIButton(leftPanelObj.transform, "Btn_StepNext", "Next Step >", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnNormal = CreateUIButton(leftPanelObj.transform, "Btn_Normal", "Normal State", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnCue = CreateUIButton(leftPanelObj.transform, "Btn_Cue", "InjectCue (Highlight)", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnHint = CreateUIButton(leftPanelObj.transform, "Btn_Hint", "InsertHint (Violet)", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnSlow = CreateUIButton(leftPanelObj.transform, "Btn_Slow", "SlowPace (Blue)", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnSimplify = CreateUIButton(leftPanelObj.transform, "Btn_Simplify", "SimplifyStep (Amber)", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnTrackToggle = CreateUIButton(leftPanelObj.transform, "Btn_TrackToggle", "Toggle Tracking", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnComplete = CreateUIButton(leftPanelObj.transform, "Btn_Complete", "Complete Task", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnReset = CreateUIButton(leftPanelObj.transform, "Btn_Reset", "Reset Preview", btnY, btnH); btnY -= (btnH + btnGap);
            Button btnDebug = CreateUIButton(leftPanelObj.transform, "Btn_Debug", "Dev Telemetry (D)", btnY, btnH);

            // Center Modal: Completion Overlay (strictly hidden on startup)
            GameObject completionObj = CreateUIPanel(canvasObj.transform, "CompletionModal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 260), new Color(0.03f, 0.06f, 0.10f, 0.96f));
            CreateUIText(completionObj.transform, "CompTitle", "TASK COMPLETE!", 24, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -35), new Vector2(0, 40), new Color(0.2f, 0.95f, 0.45f));
            CreateUIText(completionObj.transform, "CompBody", "All 8 assembly steps completed successfully.\nCircuit verified with provisional software-test GPIO 13/12.\n\nAll directional guidance suppressed.", 14, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 70), new Vector2(-30, -90), new Color(0.85f, 0.90f, 0.95f));
            Button btnCompReset = CreateUIButton(completionObj.transform, "Btn_CompReset", "Restart Assembly Preview", -190f, 38f);
            completionObj.SetActive(false);

            // Link Preview Controller serialized properties
            soPreview.FindProperty("topStepBannerText").objectReferenceValue = topStepBannerTxt;
            soPreview.FindProperty("topStepTitleText").objectReferenceValue = topStepTitleTxt;
            soPreview.FindProperty("stepInstructionText").objectReferenceValue = stepInstructionTxt;
            soPreview.FindProperty("trackingStatusText").objectReferenceValue = trackingStatusTxt;
            soPreview.FindProperty("visualStateText").objectReferenceValue = visualStateTxt;
            soPreview.FindProperty("adaptationText").objectReferenceValue = adaptationTxt;
            soPreview.FindProperty("targetInfoText").objectReferenceValue = targetInfoTxt;
            soPreview.FindProperty("diagnosticsSummaryText").objectReferenceValue = diagnosticsSummaryTxt;
            soPreview.FindProperty("provisionalNoteText").objectReferenceValue = provisionalNoteTxt;

            soPreview.FindProperty("calibrationDebugPanel").objectReferenceValue = debugPanelObj;
            soPreview.FindProperty("calibrationDebugText").objectReferenceValue = debugTmp;
            soPreview.FindProperty("debugToggleButton").objectReferenceValue = btnDebug;

            soPreview.FindProperty("prevButton").objectReferenceValue = btnStepPrev;
            soPreview.FindProperty("nextButton").objectReferenceValue = btnStepNext;
            soPreview.FindProperty("normalButton").objectReferenceValue = btnNormal;
            soPreview.FindProperty("injectCueButton").objectReferenceValue = btnCue;
            soPreview.FindProperty("insertHintButton").objectReferenceValue = btnHint;
            soPreview.FindProperty("slowPaceButton").objectReferenceValue = btnSlow;
            soPreview.FindProperty("simplifyStepButton").objectReferenceValue = btnSimplify;
            soPreview.FindProperty("trackingToggleButton").objectReferenceValue = btnTrackToggle;
            soPreview.FindProperty("completeButton").objectReferenceValue = btnComplete;
            soPreview.FindProperty("resetButton").objectReferenceValue = btnReset;

            soPreview.FindProperty("completionPanel").objectReferenceValue = completionObj;
            soPreview.FindProperty("btnCompletionReset").objectReferenceValue = btnCompReset;
            soPreview.ApplyModifiedPropertiesWithoutUndo();

            // Save the scene cleanly
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[PreviewSceneBuilder] Successfully built and saved " + ScenePath);
        }

        private static void CreateRailStripe(Transform parent, string name, Vector3 localPos, Color color)
        {
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = name;
            stripe.transform.SetParent(parent, false);
            stripe.transform.localPosition = localPos;
            stripe.transform.localScale = new Vector3(0.96f, 0.02f, 0.02f);
            Material mat = ProceduralMeshUtility.CreateMaterial(color, false, false);
            stripe.GetComponent<MeshRenderer>().material = mat;
        }

        private static void CreateTerminalSocketRows(Transform parent)
        {
            // Subtle dark socket stripes indicating terminal strips
            float[] zOffsets = new float[] { 0.26f, 0.14f, 0.06f, -0.06f, -0.14f, -0.26f };
            Material socketMat = ProceduralMeshUtility.CreateMaterial(new Color(0.75f, 0.75f, 0.73f), false, false);

            for (int i = 0; i < zOffsets.Length; i++)
            {
                GameObject rowObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rowObj.name = $"Sockets_Bank_{i}";
                rowObj.transform.SetParent(parent, false);
                rowObj.transform.localPosition = new Vector3(0f, 0.505f, zOffsets[i]);
                rowObj.transform.localScale = new Vector3(0.92f, 0.01f, 0.04f);
                rowObj.GetComponent<MeshRenderer>().material = socketMat;
            }
        }

        private static void CreateMountingHole(Transform parent, string name, Vector3 localPos)
        {
            GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hole.name = name;
            hole.transform.SetParent(parent, false);
            hole.transform.localPosition = localPos;
            hole.transform.localScale = new Vector3(0.0024f, 0.0006f, 0.0024f);
            Material holeMat = ProceduralMeshUtility.CreateMaterial(new Color(0.20f, 0.22f, 0.25f), false, false);
            hole.GetComponent<MeshRenderer>().material = holeMat;
        }

        private static void CreateTactileButton(Transform parent, string name, Vector3 localPos)
        {
            GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btn.name = name;
            btn.transform.SetParent(parent, false);
            btn.transform.localPosition = localPos;
            btn.transform.localScale = new Vector3(0.003f, 0.002f, 0.003f);
            Material mat = ProceduralMeshUtility.CreateMaterial(new Color(0.65f, 0.68f, 0.72f), false, false);
            btn.GetComponent<MeshRenderer>().material = mat;
        }

        private static void CreateColorBand(Transform parent, string name, float yOffset, Color color)
        {
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = name;
            band.transform.SetParent(parent, false);
            band.transform.localPosition = new Vector3(0f, yOffset, 0f);
            band.transform.localScale = new Vector3(1.05f, 0.12f, 1.05f);
            Material mat = ProceduralMeshUtility.CreateMaterial(color, false, false);
            band.GetComponent<MeshRenderer>().material = mat;
        }

        private static void CreateWireLead(Transform parent, string name, Vector3 localPos, float height)
        {
            GameObject lead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lead.name = name;
            lead.transform.SetParent(parent, false);
            lead.transform.localPosition = localPos;
            lead.transform.localScale = new Vector3(0.0008f, height * 0.5f, 0.0008f);
            Material leadMat = ProceduralMeshUtility.CreateMaterial(new Color(0.75f, 0.78f, 0.82f), false, false);
            lead.GetComponent<MeshRenderer>().material = leadMat;
        }

        private static GameObject CreateAnchor(Transform parent, string name, Vector3 localPos)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPos;
            return anchor;
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color bgCol)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            Image img = obj.AddComponent<Image>();
            img.color = bgCol;
            return obj;
        }

        private static TMP_Text CreateUIText(Transform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            TMP_Text tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color;
            return tmp;
        }

        private static Button CreateUIButton(Transform parent, string name, string label, float anchoredPosY, float height)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, anchoredPosY);
            rt.sizeDelta = new Vector2(200, height);

            Image img = obj.AddComponent<Image>();
            img.color = new Color(0.10f, 0.15f, 0.22f, 0.95f);
            Button btn = obj.AddComponent<Button>();

            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.12f, 0.18f, 0.26f, 0.95f);
            cb.highlightedColor = new Color(0.0f, 0.70f, 0.90f, 0.95f);
            cb.pressedColor = new Color(0.0f, 0.45f, 0.65f, 1.0f);
            btn.colors = cb;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }
    }
}
