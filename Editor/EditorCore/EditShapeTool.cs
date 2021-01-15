using System.Linq;
using UnityEngine;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.Shapes;
using Math = UnityEngine.ProBuilder.Math;
using Object = UnityEngine.Object;

using FaceData = UnityEditor.ProBuilder.EditorShapeUtility.FaceData;
using Plane = UnityEngine.ProBuilder.Shapes.Plane;
using Sprite = UnityEngine.ProBuilder.Shapes.Sprite;

#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools.EditorTools;
#else
using ToolManager = UnityEditor.EditorTools.ToolManager;
#endif

namespace UnityEditor.ProBuilder
{
    [EditorTool("Edit Shape", typeof(ShapeComponent))]
    sealed class EditShapeTool : EditorTool
    {
        Editor m_ShapeEditor;

        static readonly Color k_BoundsHandleColor = new Color(.2f, .4f, .8f, 1f);

        GUIContent m_OverlayTitle;

        static FaceData[] s_Faces;

        public static FaceData[] Faces
        {
            get
            {
                if(s_Faces == null)
                {
                    s_Faces = new FaceData[6];
                    for(int i = 0; i < s_Faces.Length; i++)
                        s_Faces[i] = new FaceData();
                }
                return s_Faces;
            }
        }

        static bool s_UpdateDrawShapeTool = false;

        //Handle Manipulation
        static int s_CurrentId = -1;
        static int[] s_OrientationControlIDs = new int[4];

        //Size Handle management
        static Vector2 s_LastMousePosition;
        static Vector3 s_SizeDelta;
        static Vector3 s_SizeLeftOver;

        //Orientation Handle Manipulation
        static float s_CurrentAngle = 0;
        static int s_CurrentArrowHovered = -1;
        static Quaternion s_ShapeRotation = Quaternion.identity;
        static Vector3[][] s_ArrowsLines = new Vector3[4][];

        public override GUIContent toolbarIcon
        {
            get { return PrimitiveBoundsHandle.editModeButton; }
        }

        void OnEnable()
        {
            m_OverlayTitle = new GUIContent("Edit Shape Tool");
            for(int i = 0; i < s_ArrowsLines.Length; i++)
                s_ArrowsLines[i] = new Vector3[3];

#if !UNITY_2020_2_OR_NEWER
            ToolManager.activeToolChanging += ActiveToolChanging;
#endif
            ProBuilderEditor.selectModeChanged += OnSelectModeChanged;

        }

        void OnDisable()
        {
#if !UNITY_2020_2_OR_NEWER
            ToolManager.activeToolChanging -= ActiveToolChanging;
#endif
            ProBuilderEditor.selectModeChanged -= OnSelectModeChanged;
            if(m_ShapeEditor != null)
                DestroyImmediate(m_ShapeEditor);
        }

#if !UNITY_2020_2_OR_NEWER
        public void ActiveToolChanging()
        {
            if(ToolManager.IsActiveTool(this))
                EditorApplication.delayCall += () => ProBuilderEditor.selectMode = SelectMode.Object;
        }
#else
        public override void OnActivated()
        {
            base.OnActivated();
            EditorApplication.delayCall += () => ProBuilderEditor.selectMode = SelectMode.Object;
        }

        public override void OnWillBeDeactivated()
        {
            base.OnWillBeDeactivated();
            EditorApplication.delayCall += () => ProBuilderEditor.ResetToLastSelectMode();
        }
#endif

        public void OnSelectModeChanged(SelectMode selectMode)
        {
            if(ToolManager.IsActiveTool(this) && selectMode != SelectMode.Object)
                ToolManager.RestorePreviousTool();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            SceneViewOverlay.Window( m_OverlayTitle, OnOverlayGUI, 0, SceneViewOverlay.WindowDisplayOption.OneWindowPerTitle );

            if(Event.current.type == EventType.MouseMove)
            {
                SceneView.RepaintAll();
                return;
            }

            foreach(var obj in targets)
            {
                var shape = obj as ShapeComponent;

                if (shape != null && !shape.edited)
                    DoEditingGUI(shape);
            }
        }

        void OnOverlayGUI(Object obj, SceneView view)
        {
            Editor.CreateCachedEditor(targets.ToArray(), typeof(ShapeComponentEditor), ref m_ShapeEditor);
            ( (ShapeComponentEditor) m_ShapeEditor ).DrawShapeGUI(null);
            ( (ShapeComponentEditor) m_ShapeEditor ).DrawShapeParametersGUI(null);

            EditorSnapSettings.gridSnapEnabled = EditorGUILayout.Toggle("Snap To Grid", EditorSnapSettings.gridSnapEnabled);
        }

        internal static void DoEditingGUI(ShapeComponent shapeComponent, bool updateDrawShapeTool = false)
        {
            if(shapeComponent == null)
                return;

            s_UpdateDrawShapeTool = updateDrawShapeTool;

            var scale = shapeComponent.transform.lossyScale;
            var position = shapeComponent.transform.position
                           + Vector3.Scale(shapeComponent.transform.TransformDirection(shapeComponent.shapeBox.center),scale);
            var matrix = Matrix4x4.TRS(position, shapeComponent.transform.rotation, Vector3.one);

            using (new Handles.DrawingScope(matrix))
            {
                EditorShapeUtility.UpdateFaces(shapeComponent.editionBounds, scale, Faces);

                for(int i = 0; i <4; ++i)
                    s_OrientationControlIDs[i] = GUIUtility.GetControlID(FocusType.Passive);

                var absSize = Math.Abs(shapeComponent.editionBounds.size);
                if(absSize.x > Mathf.Epsilon && absSize.y > Mathf.Epsilon && absSize.z > Mathf.Epsilon )
                    DoOrientationHandlesGUI(shapeComponent);

                DoSizeHandlesGUI(shapeComponent);
            }
        }

        static void DoSizeHandlesGUI(ShapeComponent shapeComponent)
        {
            int faceCount = s_Faces.Length;

            var evt = Event.current;

            var is2D = shapeComponent.shape is Plane || shapeComponent.shape is Sprite;
            for(int i = 0; i < faceCount; i++)
            {
                var face = Faces[i];
                if(is2D && !face.IsValid)
                    continue;

                if(Event.current.type == EventType.Repaint)
                {
                    Color color = k_BoundsHandleColor;
                    color.a *= face.IsVisible ? 1f : 0.5f;
                    using(new Handles.DrawingScope(color))
                    {
                        int pointsCount = face.Points.Length;
                        for(int k = 0; k < pointsCount; k++)
                            Handles.DrawLine(face.Points[k], face.Points[( k + 1 ) % pointsCount]);
                    }
                }

                if( DoFaceSizeHandle(face))
                {
                    float modifier = 1f;
                    if(Event.current.alt)
                        modifier = 2f;

                    var shapeSizeSigns = Math.Sign(shapeComponent.size);

                    var scale = shapeComponent.transform.lossyScale;
                    var scaleSigns = Math.Sign(scale);
                    var scaleInverse = new Vector3(1f/scale.x, 1f/scale.y, 1f/scale.z);

                    var delta = s_SizeDelta - s_LastDelta;
                    var sizeDelta = delta.x + delta.y + delta.z;

                    delta = Vector3.Scale(Vector3.Scale(sizeDelta * Math.Abs(s_Faces[i].Normal), scaleInverse) , Vector3.Scale(scale,shapeSizeSigns));
                    delta += s_SizeLeftOver;

                    var sizeOffset = ProBuilderSnapping.Snap(modifier * delta, evt.shift? EditorSnapping.incrementalSnapMoveValue : Vector3.zero);
                    s_SizeLeftOver = modifier * delta - sizeOffset;

                    var faceNormal = shapeComponent.transform.TransformVector(s_Faces[i].Normal);
                    var center = Event.current.alt ? Vector3.zero : Vector3.Scale(Mathf.Sign(sizeDelta)*(sizeOffset.magnitude / 2f) * faceNormal , scaleSigns);
                    var currentCenter = shapeComponent.transform.position + Vector3.Scale(shapeComponent.transform.TransformVector(shapeComponent.shapeBox.center),scale);

                    ApplyProperties(shapeComponent, currentCenter + center, shapeComponent.size + sizeOffset);
                }
            }
        }

        static Vector3 s_LastDelta;
        static float s_DefaultMidpointHandleSize = 0.03f;
        static float s_DefaultMidpointSquareSize = 0.15f;

        static bool DoFaceSizeHandle(FaceData face)
        {
            if( s_OrientationControlIDs.Contains(HandleUtility.nearestControl) && !EditorShapeUtility.PointerIsInFace(face) )
                return false;

            float handleSize = HandleUtility.GetHandleSize(face.CenterPosition) * s_DefaultMidpointHandleSize;

            var snap = Vector3.Scale(EditorSnapping.incrementalSnapMoveValue, face.Normal).magnitude;

            EditorGUI.BeginChangeCheck();

            Color color = k_BoundsHandleColor;
            color.a *= face.IsVisible ? 1f : 0.25f;
            Vector3 newTargetPosition;
            using(new Handles.DrawingScope(color))
                newTargetPosition = Handles.Slider(face.CenterPosition, face.Normal, handleSize, Handles.DotHandleCap, snap);

            if (EditorGUI.EndChangeCheck())
            {
                if(s_LastDelta.Equals(Vector3.negativeInfinity))
                    s_LastDelta = Vector3.Scale(face.CenterPosition, face.Normal);
                else
                    s_LastDelta = s_SizeDelta;

                s_SizeDelta = Vector3.Scale(newTargetPosition, face.Normal);
                s_LastDelta *= Vector3.Dot(s_LastDelta.normalized, s_SizeDelta.normalized);

                return true;
            }

            if(GUIUtility.hotControl == 0)
                s_LastDelta = Vector3.negativeInfinity;

            return false;
        }

        static void DoOrientationHandlesGUI(ShapeComponent shapeComponent)
        {
            if( GUIUtility.hotControl != 0 && !s_OrientationControlIDs.Contains(GUIUtility.hotControl) )
                return;

            foreach(var f in Faces)
            {
                if(f.IsVisible && EditorShapeUtility.PointerIsInFace(f))
                {
                    if(DoOrientationHandle(f, shapeComponent))
                    {
                        UndoUtility.RecordComponents<Transform, ProBuilderMesh, ShapeComponent>(shapeComponent.GetComponents(typeof(Component)),"Rotate Shape");
                        shapeComponent.RotateInsideBounds(s_ShapeRotation);

                        //Only Updating Draw shape tool when using this tool
                        if(s_UpdateDrawShapeTool)
                            DrawShapeTool.s_LastShapeRotation = shapeComponent.rotation;

                        ProBuilderEditor.Refresh();
                    }
                }
            }

        }

        static bool DoOrientationHandle(FaceData face, ShapeComponent shapeComponent)
        {
            Event evt = Event.current;
            bool hasRotated = false;

            float handleSize = HandleUtility.GetHandleSize(Vector3.zero);
            var scaleModifier = Math.Sign(shapeComponent.size);

            switch(evt.type)
            {
                case EventType.MouseDown:
                    if ( s_OrientationControlIDs.Contains(HandleUtility.nearestControl) && evt.button == 0 )
                    {
                        s_CurrentId = HandleUtility.nearestControl;
                        GUIUtility.hotControl = s_CurrentId;
                        evt.Use();
                    }
                   break;
                case EventType.MouseUp:
                    if (s_OrientationControlIDs.Contains(HandleUtility.nearestControl) && evt.button == 0 )
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                        if(s_CurrentId == HandleUtility.nearestControl)
                        {
                            //Execute rotation
                            Vector3 targetedNormal = Vector3.zero;
                            for(int i = 0; i < s_OrientationControlIDs.Length; i++)
                            {
                                if(s_OrientationControlIDs[i] == s_CurrentId)
                                {
                                    targetedNormal = (s_ArrowsLines[i][1] - face.CenterPosition).normalized;
                                    break;
                                }
                            }

                            targetedNormal = Vector3.Scale(targetedNormal, scaleModifier);
                            var currentNormal = Vector3.Scale(face.Normal, scaleModifier);
                            Vector3 rotationAxis = Vector3.Cross(currentNormal,targetedNormal);
                            var angle = Vector3.SignedAngle(currentNormal, targetedNormal, rotationAxis);
                            s_ShapeRotation = Quaternion.AngleAxis(angle, rotationAxis);
                            s_CurrentAngle = (s_CurrentAngle + angle) % 360;

                            hasRotated = true;
                        }
                        s_CurrentId = -1;
                    }
                    break;
                    case EventType.Layout:
                        for(int i = 0; i < 4; i++)
                        {
                            var rectPos = 0.8f * s_ArrowsLines[i][1] + 0.2f * face.CenterPosition;
                            float dist = HandleUtility.DistanceToRectangle( rectPos,
                                Quaternion.LookRotation(face.Normal),
                                HandleUtility.GetHandleSize(face.CenterPosition) * s_DefaultMidpointSquareSize/2f);
                            HandleUtility.AddControl(s_OrientationControlIDs[i], dist);
                        }
                        break;
                   case EventType.Repaint:
                        if(s_CurrentArrowHovered != HandleUtility.nearestControl)
                           s_CurrentAngle = 0f;

                       int pointsCount = face.Points.Length;
                       s_CurrentArrowHovered = -1;
                       for(int i = 0; i < pointsCount; i++)
                       {
                           var rectHandleSize = HandleUtility.GetHandleSize(face.CenterPosition) * s_DefaultMidpointSquareSize;

                           var sideDirection = ( face.Points[( i + 1 ) % pointsCount] - face.Points[i] ).normalized;
                           var arrowDirection = Vector3.Cross(face.Normal.normalized, sideDirection).normalized;

                           var topDirection = 2.5f * rectHandleSize * arrowDirection;
                           var top = face.CenterPosition + topDirection;
                           var A = topDirection.magnitude;
                           var a = 0.33f * Mathf.Sqrt(2f * A * A);
                           var h = 0.5f * Mathf.Sqrt(2f * a * a);
                           s_ArrowsLines[i][0] = top - ( h * arrowDirection + h * sideDirection );
                           s_ArrowsLines[i][1] = top;
                           s_ArrowsLines[i][2] = top - ( h * arrowDirection - h * sideDirection );

                           bool selected = HandleUtility.nearestControl == s_OrientationControlIDs[i];

                           Color color = selected
                               ? EditorHandleDrawing.edgeSelectedColor
                               : k_BoundsHandleColor;
                           color.a = 1.0f;

                           using(new Handles.DrawingScope(color))
                           {
                               Handles.DrawAAPolyLine(5f, s_ArrowsLines[i]);
                               if(selected)
                               {
                                   EditorGUIUtility.AddCursorRect(new Rect(0,0,Screen.width, Screen.height), MouseCursor.RotateArrow);
                                   s_CurrentArrowHovered = HandleUtility.nearestControl;
                                   Handles.DrawAAPolyLine(3f,
                                       new Vector3[]
                                       {
                                           Vector3.Scale(shapeComponent.rotation * Vector3.up, shapeComponent.size / 2f),
                                           Vector3.zero,
                                           Vector3.Scale(shapeComponent.rotation * Vector3.forward, shapeComponent.size / 2f)
                                       });
                               }
                           }
                       }
                        break;
                case EventType.MouseDrag:
                    if(s_OrientationControlIDs.Contains(s_CurrentId) && HandleUtility.nearestControl != s_CurrentId)
                    {
                        GUIUtility.hotControl = 0;
                        s_CurrentId = -1;
                    }
                    break;
             }
             return hasRotated;
        }

        public static void ApplyProperties(ShapeComponent shape, Vector3 newCenterPosition, Vector3 newSize)
        {
            var bounds = new Bounds();
            bounds.center = newCenterPosition;
            bounds.size = newSize;

            UndoUtility.RecordComponents<Transform, ProBuilderMesh, ShapeComponent>(shape.GetComponents(typeof(Component)),"Resize Shape");
            shape.UpdateBounds(bounds);

            ProBuilderEditor.Refresh(false);
        }

    }
}
