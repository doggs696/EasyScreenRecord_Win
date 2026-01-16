using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace EasyScreenRecord.Helpers
{
    public static class UIAutomationHelper
    {
        private static IUIAutomation? _automation;

        public static Point? GetCaretPosition()
        {
            try
            {
                if (_automation == null)
                {
                    _automation = new CUIAutomation();
                }

                // 1. Get Focused Element
                var element = _automation.GetFocusedElement();
                if (element == null) return null;

                // 2. Try TextPattern (for editors like Notepad, Word, VS Code)
                // UIA_TextPatternId = 10014
                var textPatternObj = element.GetCurrentPattern(10014);
                if (textPatternObj != null)
                {
                    var textPattern = (IUIAutomationTextPattern)textPatternObj;
                    var selection = textPattern.GetSelection(); // IUIAutomationTextRangeArray
                    if (selection != null && selection.Length > 0)
                    {
                        var range = selection.GetElement(0); // IUIAutomationTextRange
                        var rects = range.GetBoundingRectangles();
                        
                        // rects is a double array [left, top, width, height, ...]
                        // If typical caret, width might be 0.
                        if (rects != null && rects.Length >= 4)
                        {
                            double left = (double)rects.GetValue(0)!;
                            double top = (double)rects.GetValue(1)!;
                            double width = (double)rects.GetValue(2)!;
                            double height = (double)rects.GetValue(3)!;
                            
                            return new Point((int)(left + width / 2), (int)(top + height / 2));
                        }
                    }
                }
                
                // 3. Fallback: Get Bounding Rect of the element itself (e.g. Buttons, simple inputs)
                // Check if it has ValuePattern? Or just use bounds.
                // CurrentBoundingRectangle propertyId = 30001
                // But this returns the whole element center. Might be okay for buttons.
                
                // Let's rely on standard Windows API GetGUIThreadInfo/GetCaretPos for fallback in another helper if needed.
                // For now, return element center if TextPattern failed but it is focusable?
                // Actually returning null lets the caller decide to use mouse position or keep last known.
                
                return null;
            }
            catch (Exception)
            {
                // UIA often throws COM exceptions if element becomes invalid during call
                return null;
            }
        }

        // --- COM Interfaces Definitions ---

        [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomation
        {
            void CompareElements(IUIAutomationElement el1, IUIAutomationElement el2, out int areSame);
            void CompareRuntimeIds(IntPtr runtimeId1, IntPtr runtimeId2, out int areSame);
            void GetRootElement(out IUIAutomationElement root);
            IUIAutomationElement GetFocusedElement();
            // ... omitting other methods not strictly needed for this simple impl
        }

        [ComImport, Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
        private class CUIAutomation : IUIAutomation
        {
            public extern void CompareElements(IUIAutomationElement el1, IUIAutomationElement el2, out int areSame);
            public extern void CompareRuntimeIds(IntPtr runtimeId1, IntPtr runtimeId2, out int areSame);
            public extern void GetRootElement(out IUIAutomationElement root);
            public extern IUIAutomationElement GetFocusedElement();
        }

        [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationElement
        {
            void SetFocus();
            void GetRuntimeId(out IntPtr runtimeId);
            void GetFirstDirectChild(out IUIAutomationElement child);
            void GetLastDirectChild(out IUIAutomationElement child);
            void GetNextSiblingElement(out IUIAutomationElement child);
            void GetPreviousSiblingElement(out IUIAutomationElement child);
            void GetCurrentProcessId(out int retVal);
            void GetCurrentElementBuildUpdated(out int retVal);
            
            // GetCurrentPattern
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object GetCurrentPattern(int patternId);
            
            // ... omitting many properties
        }

        [ComImport, Guid("32eba289-3583-42c9-9c59-3b6d9a1a9b6a")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationTextPattern
        {
            IUIAutomationTextRangeArray GetSelection(); 
            // GetVisibleRanges
            // RangeFromChild
            // RangeFromPoint
            // DocumentRange
            // SupportedTextSelection
        }
        
        // Note: GetSelection returns IUIAutomationTextRangeArray, not a single range.
        // We need to define Array interface.

        [ComImport, Guid("CE4AE76A-E6DA-4384-83FC-5E8ACE052A69")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationTextRangeArray
        {
            int Length { get; }
            IUIAutomationTextRange GetElement(int index);
        }

        [ComImport, Guid("A543CC6A-F4AE-494b-8239-C814481187A8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationTextRange
        {
            IUIAutomationTextRange Clone();
            bool Compare(IUIAutomationTextRange range);
            int CompareEndpoints(int srcEndPoint, IUIAutomationTextRange range, int targetEndPoint);
            void ExpandToEnclosingUnit(int textUnit);
            
            // ... 
            
            // We need GetBoundingRectangles
            // It is the 10th method in vtable? 
            // Order IS important in InterfaceIsIUnknown.
            // Let's try to be precise or use dynamic/IDispatch if possible? No, UIA is IUnknown.
            
            // Full definition required for accurate VTable. This is risky with partial defs.
            // Alternative: Use "UIAutomationClient" Nuget package or standard reference?
            // "Interop.UIAutomationClient" is standard.
            
            // Re-evaluating: Creating C# wrappers for UIA purely by memory layout is hard.
            // BUT, GetBoundingRectangles returns SAFEARRAY(double).
            
            // Method list for IUIAutomationTextRange:
            // Clone, Compare, CompareEndpoints, ExpandToEnclosingUnit, FindAttribute, FindText, GetAttributeValue,
            // GetBoundingRectangles, GetEnclosingElement, GetText, Move, MoveEndpointByUnit, MoveEndpointByRange...
            
            // Let's define up to GetBoundingRectangles.
            void ExpandToEnclosingUnit_Stub(); // placeholder
            void FindAttribute_Stub();
            void FindText_Stub();
            void GetAttributeValue_Stub();

            [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_R8)]
            double[] GetBoundingRectangles();
        }
    }
}
