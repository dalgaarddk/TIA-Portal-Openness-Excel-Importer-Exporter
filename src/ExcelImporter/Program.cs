﻿using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Shapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Flashing;
using Siemens.Engineering.HmiUnified.UI;
using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Tag;
using UnifiedOpennessLibrary;

namespace ExcelImporter
{
    public static class Globals
    {
        public static String oldPropertyName = ""; // Modifiable
        public static ScriptDynamization scriptTemp= null;
    }
    class Program
    {
        public static UnifiedOpennessConnector unifiedData = null;
        static void Main(string[] args)
        {
            using (var unifiedData = new UnifiedOpennessConnector("V20", args, new List<CmdArgument>(), "ExcelImporter"))
            {
                Program.unifiedData = unifiedData;
                Work();
            }
            unifiedData.Log("Import finished");
        }

        static void Work()
        {
            //2.Open Excel sheet
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xlsx").Where<string>(x => !Path.GetFileName(x).StartsWith("~$"));
            //iterate over every file
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                Microsoft.Office.Interop.Excel.Application xlApp = null;
                Microsoft.Office.Interop.Excel.Workbook workbook = null;
                Microsoft.Office.Interop.Excel.Range range = null;
                Microsoft.Office.Interop.Excel.Worksheet worksheet = null;

                try
                {
                    xlApp = new Microsoft.Office.Interop.Excel.Application();
                    workbook = xlApp.Workbooks.Open((Directory.GetCurrentDirectory() + "\\" + filename));
                    worksheet = (Microsoft.Office.Interop.Excel.Worksheet)workbook.Worksheets[1];
                    range = worksheet.UsedRange;

                    if (!File.Exists(file))
                    {
                        unifiedData.Log("Cannot find a file with name '" + filename + "' in path '" + Directory.GetCurrentDirectory() + "'", LogLevel.Error);
                        unifiedData.Log("Please place a file with name '" + filename + "' next to the app!", LogLevel.Error);
                        return;
                    }

                    // get screen filename=foo.xlsx
                    var screenName = filename.Split('.')[0];
                    var screen = unifiedData.Screens.FirstOrDefault(s => s.Name == screenName);
                    if (screen == null)
                    {
                        screen = unifiedData.UnifiedSoftware.Screens.Create(screenName);
                        unifiedData.Log("New screen with name '" + screenName + "' added.", LogLevel.Debug);
                    }
                    else
                    {
                        unifiedData.Log("Found screen with name '" + screenName + "'.", LogLevel.Debug);
                    }
                    // 3. Read Excel file and add elements to TIA portal
                    int tableColumn = 1;
                    int tableRow = 2;

                    while (true)
                    {
                        if (worksheet.Cells[tableRow, 1].Value2 == null || worksheet.Cells[tableRow, 1].Value2 == "")
                        {
                            break; // end of file
                        }
                        Dictionary<string, object> propertyNameValues = new Dictionary<string, object>();
                        while (true)
                        {
                            //Check if cell is empty and add to dictonary
                            if (worksheet.Cells[tableRow, tableColumn].Value2 == null)
                            {
                                propertyNameValues.Add(worksheet.Cells[1, tableColumn].Value2.ToString(), "");
                            }
                            else
                            {
                                propertyNameValues.Add(worksheet.Cells[1, tableColumn].Value2.ToString(), worksheet.Cells[tableRow, tableColumn].Value2);
                            }

                            tableColumn++;
                            //check if all attributes are read in and Create the Screen Item 
                            if (worksheet.Cells[1, tableColumn].Value2 == null || worksheet.Cells[1, tableColumn].Value2 == "")
                            {
                                tableColumn = 1;
                                break;
                            }
                        }
                        CreateScreenItem(screen, propertyNameValues);
                        Console.WriteLine();
                        tableRow++;
                    }

                    for (int tableRow_ = 2; worksheet.Cells[tableRow_, 1].Value2 != null; tableRow_++)
                    {
                        for (int tableColumn_ = 1; worksheet.Cells[1, tableColumn_].Value2 != null; tableColumn_++)
                        { }
                    }

                }
                finally
                {
                    workbook.Close();
                    xlApp.Quit();
                    Marshal.ReleaseComObject(xlApp);
                    Marshal.ReleaseComObject(workbook);
                    Marshal.ReleaseComObject(worksheet);
                    Marshal.ReleaseComObject(range);
                    xlApp = null;
                    workbook = null;
                    worksheet = null;
                    range = null;
                }
            }
        }

        static void CreateScreenItem(HmiScreen screen, Dictionary<string, object> propertyNameValues)
        {
            string sName = propertyNameValues["Name"].ToString();
            propertyNameValues.Remove("Name");
            string sType = propertyNameValues["Type"].ToString();
            propertyNameValues.Remove("Type");
            unifiedData.Log("CreateScreenItem: " + sName + " of type " + sType, LogLevel.Debug);
            Type type = null;
            if (sType == "HmiLine" || sType == "HmiPolyline" || sType == "HmiPolygon" || sType == "HmiEllipse" || sType == "HmiEllipseSegment"
                || sType == "HmiCircleSegment" || sType == "HmiEllipticalArc" || sType == "HmiCircularArc" || sType == "HmiCircle" || sType == "HmiRectangle"
                || sType == "HmiGraphicView" || sType == "HmiText")
            {
                type = Type.GetType("Siemens.Engineering.HmiUnified.UI.Shapes." + sType + ", Siemens.Engineering");
            }
            else if (sType == "HmiIOField" || sType == "HmiButton" || sType == "HmiToggleSwitch" || sType == "HmiCheckBoxGroup" || sType == "HmiBar"
                || sType == "HmiGauge" || sType == "HmiSlider" || sType == "HmiRadioButtonGroup" || sType == "HmiListBox" || sType == "HmiClock"
                || sType == "HmiTextBox")
            {
                type = Type.GetType("Siemens.Engineering.HmiUnified.UI.Widgets." + sType + ", Siemens.Engineering");
            }
            else if (sType == "HmiAlarmControl" || sType == "HmiMediaControl" || sType == "HmiTrendControl" || sType == "HmiTrendCompanion"
                || sType == "HmiProcessControl" || sType == "HmiFunctionTrendControl" || sType == "HmiWebControl" || sType == "HmiDetailedParameterControl" || sType == "HmiFaceplateContainer")
            {
                type = Type.GetType("Siemens.Engineering.HmiUnified.UI.Controls." + sType + ", Siemens.Engineering");
            }
            else if (sType == "HmiScreenWindow")
            {
                type = Type.GetType("Siemens.Engineering.HmiUnified.UI.Screens." + sType + ", Siemens.Engineering");
            }
            else if (sType == "HmiScreen")
            {
                // to prevent returning from this function without doing nothing
            }
            else if (sType.ToLower() == "pause")
            {
                unifiedData.Log("Progress paused due to command '" + sType + "'. Please hit any key to continue...");
                Console.Read();
                return;
            }
            else {
                unifiedData.Log("ScreenItem with type " + sType + " is not implemented yet!", LogLevel.Warning);
                return;
            }

            UIBase screenItem = screen;
            if (sType != "HmiScreen")
            {
                screenItem = screen.ScreenItems.Find(sName);
            }
            if (screenItem == null)
            {
                MethodInfo createMethod = typeof(HmiScreenItemBaseComposition).GetMethod("Create", BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, new Type[] { typeof(string) }, null);
                MethodInfo generic = createMethod.MakeGenericMethod(type);
                screenItem = (HmiScreenItemBase)generic.Invoke(screen.ScreenItems, new object[] { sName });
            }

            foreach (var propertyNameValue in propertyNameValues)
            {
                if (propertyNameValue.Value.ToString() == "")
                {
                    continue;
                }
                else { 
                    unifiedData.Log("Will try to set Property '" + propertyNameValue.Key + "' with value '" + propertyNameValue.Value + "'.", LogLevel.Debug);
                    try
                    {
                        //cover # Attributes
                        if (propertyNameValue.Key.Contains("Property") && propertyNameValue.Key.Contains("Events"))
                        {
                            SetChangeEvent(screenItem, propertyNameValue.Key, propertyNameValue.Value);
                        }
                        else if (propertyNameValue.Key.Contains("Events"))
                        {
                            string key = propertyNameValue.Key.Split('.')[1] + '.' + propertyNameValue.Key.Split('.')[2];
                            SetEvent(screenItem, key, propertyNameValue.Value);
                        }
                        else if (propertyNameValue.Key.Contains("Dynamization"))
                        {
                            SetDynamization(screenItem, propertyNameValue.Key, propertyNameValue.Value);
                        }
                        else
                        {
                            SetPropertyRecursive(propertyNameValue.Key, propertyNameValue.Value.ToString(), screenItem);
                        }

                    }
                    catch (Exception ex)
                    {
                        unifiedData.Log("Cannot set property '" + propertyNameValue.Key + "' with value '" + propertyNameValue.Value + "'.", LogLevel.Warning);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="screenItem"></param>
        /// <param name="key">Down#ScriptCode or Down#Async</param>
        /// <param name="value"></param>
        private static void SetEvent(UIBase screenItem, string key, object value)
        {
            var comp = (screenItem as IEngineeringObject).GetComposition("EventHandlers") as IEngineeringComposition;
            MethodBase createMethod = comp.GetType().GetMethod("Create");
            MethodBase findMethod = comp.GetType().GetMethod("Find");

            //split key
            string[] keys = key.Split('.');
            foreach (var item in Enum.GetValues(createMethod.GetParameters()[0].ParameterType))
            {
                if (item.ToString() == keys[0])
                {
                    var eventHandler = findMethod.Invoke(comp, new object[] { item }) ?? createMethod.Invoke(comp, new object[] { item });
                    IEngineeringObject event_ = (eventHandler as IEngineeringObject).GetAttribute("Script") as IEngineeringObject;
                    SetPropertyRecursive(keys[1], value.ToString(), event_);
                }
            }
        }

        private static void SetChangeEvent(UIBase screenItem, string key, object value)
        {
            

            var comp = (screenItem as IEngineeringObject).GetComposition("PropertyEventHandlers") as IEngineeringComposition;
            MethodBase createMethod = comp.GetType().GetMethod("Create");
            MethodBase findMethod = comp.GetType().GetMethod("Find");
            string key1 = key.Split('.')[0];
            string key2 = key.Split('.')[2];

            foreach (var item in Enum.GetValues(createMethod.GetParameters()[1].ParameterType))
            {
                if (key.Split('.')[1] == "PropertyQualityCodeEvents")
                {
                        IEngineeringObject propEventHandler = null;
                        propEventHandler = screenItem.PropertyEventHandlers.Create("ProcessValue", Siemens.Engineering.HmiUnified.UI.Events.PropertyEventType.QualityCodeChange);
                        IEngineeringObject propEvent = (propEventHandler as IEngineeringObject).GetAttribute("Script") as IEngineeringObject;
                        SetPropertyRecursive(key2, value.ToString(), propEvent);

                    //else
                    //{
                    //    var propEventHandler = findMethod.Invoke(comp, new object[] { key1, item }) ?? createMethod.Invoke(comp, new object[] { key1, item });
                    //    IEngineeringObject propEvent = (propEventHandler as IEngineeringObject).GetAttribute("Script") as IEngineeringObject;
                    //    SetPropertyRecursive(key2, value.ToString(), propEvent);
                    //}

                }
                else if (item.ToString() == "Change")
                {
                        var propEventHandler = findMethod.Invoke(comp, new object[] { key1, item }) ?? createMethod.Invoke(comp, new object[] { key1, item });
                        IEngineeringObject propEvent = (propEventHandler as IEngineeringObject).GetAttribute("Script") as IEngineeringObject;
                        SetPropertyRecursive(key2, value.ToString(), propEvent);
                }

            }
        }

        //string oldPropertyName = "";
        private static void SetDynamization(UIBase screenItem, string key, object value)
        {
            var comp = (screenItem as IEngineeringObject).GetComposition("Dynamizations") as IEngineeringComposition;
            MethodBase findMethod = comp.GetType().GetMethod("Find");

            var keyLength = key.Split('.').Length;
            string[] keys = new string[keyLength];
            int dynStart = 0;
            for (int i = 0; i < keyLength; i++)
            {
                keys[i] = key.Split('.')[i];
                //find where the dynamization starts
                if (keys[i].Contains("Dynamization"))
                {
                    dynStart = i;
                }
            }
            var propertyNameList = new List<string>();
            for (int i = dynStart + 1; i < keyLength; i++)
            {
                propertyNameList.Add(keys[i]);
            }
            var propertyName = string.Join(".", propertyNameList);            

            var findDyn = findMethod.Invoke(comp, new object[] { keys[0] });
            
            // todo: make generic
            if (keys[dynStart].StartsWith("Script") && findMethod != null)
            {
                if (Globals.oldPropertyName != keys[dynStart - 1])
                {
                    Globals.scriptTemp = null;
                }

                if (screenItem is HmiFaceplateContainer)
                {
                    var fpContainer = screenItem as HmiFaceplateContainer;
                    var str = keys[0].ToString().Split('[')[1];
                    str = str.Split(']')[0];
                    int number = Convert.ToInt32(str);
                    if (Globals.oldPropertyName != keys[dynStart - 1])
                    {
                        Globals.scriptTemp = fpContainer.Interface[number].Dynamizations.Create<ScriptDynamization>(keys[dynStart - 1]);
                    }
                }
                
                else
                {
                    Globals.scriptTemp = (ScriptDynamization)findMethod.Invoke(comp, new object[] { keys[dynStart - 1] }) ?? screenItem.Dynamizations.Create<ScriptDynamization>(keys[dynStart - 1]);
                }
                if (keys[keyLength - 1].Contains("GlobalDefinition"))
                {
                    Globals.scriptTemp.GlobalDefinitionAreaScriptCode = value.ToString();
                }
                else if (keys[keyLength - 1].Contains("ScriptCode"))
                {
                    Globals.scriptTemp.ScriptCode = value.ToString();
                }

                else if (keys[keyLength - 1].Contains("Type"))
                {
                    Globals.scriptTemp.Trigger.Type = TriggerType.Tags;
                }

                else if (keys[keyLength - 1].Contains("Tags"))
                {
                    var tagCount = value.ToString().Split(',').Length;
                    List<string> tags = new List<string>();
                    for (int i = 0; i < tagCount; i++)
                    {
                        tags.Add(value.ToString().Split(',')[i]);
                    }
                    Globals.scriptTemp.Trigger.Tags = tags;
                }
                else
                {
                    SetPropertyRecursive(propertyName, value.ToString(), (Globals.scriptTemp as IEngineeringObject));
                }
            }
            else if (keys[dynStart].StartsWith("Tag") && findMethod != null)
            {
                TagDynamization temp = null;
                if (screenItem is Siemens.Engineering.HmiUnified.UI.Controls.HmiFaceplateContainer)
                {
                    var fpContainer = screenItem as HmiFaceplateContainer;
                    var str = keys[0].ToString().Split('[')[1];
                    str = str.Split(']')[0];
                    int number = Convert.ToInt32(str);
                    temp = fpContainer.Interface[number].Dynamizations.Create<TagDynamization>(keys[dynStart - 1]);
                }
                else
                {
                    temp = (TagDynamization)findMethod.Invoke(comp, new object[] { keys[dynStart - 1] }) ?? screenItem.Dynamizations.Create<TagDynamization>(keys[dynStart - 1]);
                }
                SetPropertyRecursive(propertyName, value.ToString(), (temp as IEngineeringObject));
            }
            else if (keys[dynStart].StartsWith("ResourceList") && findMethod != null)
            {
                ResourceListDynamization temp = null;
                if (screenItem is Siemens.Engineering.HmiUnified.UI.Controls.HmiFaceplateContainer)
                {
                    var fpContainer = screenItem as HmiFaceplateContainer;
                    var str = keys[0].ToString().Split('[')[1];
                    str = str.Split(']')[0];
                    int number = Convert.ToInt32(str);
                    temp = fpContainer.Interface[number].Dynamizations.Create<ResourceListDynamization>(keys[dynStart - 1]);
                }
                else
                {
                    temp = (ResourceListDynamization)findMethod.Invoke(comp, new object[] { keys[dynStart - 1] }) ?? screenItem.Dynamizations.Create<ResourceListDynamization>(keys[dynStart - 1]);
                }
                    SetPropertyRecursive(propertyName, value.ToString(), (temp as IEngineeringObject));
            }
            else if (keys[dynStart].StartsWith("Flashing") && findMethod != null)
            {
                FlashingDynamization temp = null;
                if (screenItem is Siemens.Engineering.HmiUnified.UI.Controls.HmiFaceplateContainer)
                {
                    var fpContainer = screenItem as HmiFaceplateContainer;
                    var str = keys[0].ToString().Split('[')[1];
                    str = str.Split(']')[0];
                    int number = Convert.ToInt32(str);
                    temp = fpContainer.Interface[number].Dynamizations.Create<FlashingDynamization>(keys[dynStart - 1]);
                }
                else
                {
                    temp = (FlashingDynamization)findMethod.Invoke(comp, new object[] { keys[dynStart - 1] }) ?? screenItem.Dynamizations.Create<FlashingDynamization>(keys[dynStart - 1]);
                }
                    SetPropertyRecursive(propertyName, value.ToString(), (temp as IEngineeringObject));
            }
            Globals.oldPropertyName = keys[dynStart - 1];
        }

        static public void SetMyAttributesSimpleTypes(string keyToSet, object valueToSet, IEngineeringObject obj)
        {
            Type _type = null;
            try
            {
                _type = obj.GetAttribute(keyToSet)?.GetType() ??
                obj.GetAttributeInfos().FirstOrDefault(x => x.Name == keyToSet)?.SupportedTypes.FirstOrDefault();
            } catch (EngineeringNotSupportedException)
            {
                _type = obj.GetType();
            }


            object attrVal = null;
            if (_type != null && _type.BaseType == typeof(Enum))
            {
                attrVal = Enum.Parse(_type, valueToSet.ToString());
            }
            else if (_type != null && _type.Name == "Color")
            {
                var hexColor = new ColorConverter();
                attrVal = (Color)hexColor.ConvertFromString(valueToSet.ToString().ToUpper());
            }
            else if (keyToSet == "InitialAddress")
            {
                attrVal = valueToSet.ToString().Substring(0, valueToSet.ToString().Length - 1);
            }
            else if (_type != null && _type.Name == "MultilingualText")
            {
                var multiLingText = obj as MultilingualText;
                obj = multiLingText.Items.FirstOrDefault(x => x.Language.Culture.Name == keyToSet);

                if (obj == null)
                {
                    unifiedData.Log("Cannot find a language for the text property '" + keyToSet + "'.", LogLevel.Warning);
                    return;
                }
                keyToSet = "Text";
                attrVal = valueToSet.ToString();
            }
            else if (keyToSet == "Tags")
            {
                attrVal = (valueToSet as List<object>).Select(i => i.ToString()).ToList();
            }
            else
            {
                if (_type != null) attrVal = Convert.ChangeType(valueToSet, _type);
            }

            try
            {
                obj.SetAttribute(keyToSet.ToString(), attrVal);
            }
            catch (Exception ex) {
                unifiedData.Log(ex.Message, LogLevel.Warning);
            }
            if (keyToSet == "ConditionType")
            {
                var mappingTable = obj as MappingTable;
                if (mappingTable != null)
                {
                    // remove all entries always when a new condition is recognized, because entries can be mixed today.
                    int count = mappingTable.Entries.Count;
                    for (int i = 0; i < count; i++)
                    {
                        mappingTable.Entries[0].Delete();
                    }
                }
            }
        }

        public static void SetPropertyRecursive(string key, string value, IEngineeringObject relevantTag)
        {
            if (key.Contains("."))
            {
                IEngineeringObject deeperObj = null;
                List<string> keySplit = key.Split('.').ToList();
                if (keySplit[0].EndsWith("]")) //trerndAreas[0]
                {
                    var compositionsName = keySplit[0].Split('[')[0];
                    var composition = relevantTag.GetComposition(compositionsName) as IEngineeringComposition;
                    var indexString = keySplit[0].Split('[')[1];
                    int index = int.Parse(indexString.Split(']')[0]);
                    int count = composition.Count;
                    while (count <= index)
                    {
                        if (composition is HmiPointComposition)
                        {
                            deeperObj = (composition as HmiPointComposition).Create(0, 0);
                        }
                        else if (composition is MappingTableEntryBaseComposition)
                        {
                            var comp = (composition as MappingTableEntryBaseComposition);
                            var conditionType = (comp.Parent as MappingTable).ConditionType;
                            switch (conditionType)
                            {
                                case ConditionType.Range:
                                    comp.Create<MappingTableEntryRange>();
                                    break;
                                case ConditionType.Bitmask:
                                    comp.Create(BitDynamizationType.MultiBit);
                                    break;
                                case ConditionType.Singlebit:
                                    comp.Create(BitDynamizationType.SingleBit);
                                    break;
                                case ConditionType.None:
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            Type type = composition[0].GetType();
                            deeperObj = composition.Create(type, null);
                        }
                        count++;
                    }
                    if (deeperObj == null)
                    {
                        deeperObj = composition[index];
                    }
                }
                else
                {
                    deeperObj = relevantTag.GetAttribute(keySplit[0]) as IEngineeringObject;
                }
                
                keySplit.RemoveAt(0);
                string deeperKey = string.Join(".", keySplit);
                SetPropertyRecursive(deeperKey, value, deeperObj);
            }
            else
            {
                SetMyAttributesSimpleTypes(key, value, relevantTag);
            }
        }
    }
}
