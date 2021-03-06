﻿namespace ICSSoft.STORMNET.Business
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using ICSSoft.STORMNET.Security;
    using STORMDO = ICSSoft.STORMNET;
    using STORMFunction = ICSSoft.STORMNET.FunctionalLanguage.Function;

    /// <summary>
    /// Набор служебной логики для сервиса данных.
    /// </summary>
    public partial class Utils
    {
        /// <summary>
        /// Заполнить объект данных.
        /// </summary>
        /// <param name="dobject">Объект данных.</param>
        /// <param name="values">Значения для заполнения.</param>
        /// <param name="storageStruct">Метаданные структуры хранения.</param>
        /// <param name="customizationStruct">Настройка выборки данных.</param>
        /// <param name="typesByKeys">Служебная структура, увязывающая типы мастеров и их ключи.</param>
        /// <param name="advCols">Дополнительные колонки.</param>
        /// <param name="dataObjectCache">Кэш объектов данных.</param>
        /// <param name="securityManager">Менеджер полномочий.</param>
        public static void FillRowSetToDataObject(DataObject dobject, object[] values, StorageStructForView storageStruct, LoadingCustomizationStruct customizationStruct, System.Collections.SortedList typesByKeys, AdvansedColumn[] advCols, DataObjectCache dataObjectCache, ISecurityManager securityManager)
        {
            Type dobjectType = dobject.GetType();

            /* access type */

            if (!securityManager.AccessObjectCheck(dobjectType, tTypeAccess.Full, false))
            {
                securityManager.AccessObjectCheck(dobjectType, tTypeAccess.Read, true);
            }

            /* access type */

            // Заливаем данные в объект данных.
            int customizationStructViewPropertiesLength = customizationStruct.View.Properties.Length;
            int advColsLength = advCols.Length;
            Information.SetPropValueByName(dobject, "__PrimaryKey", values[customizationStructViewPropertiesLength + advColsLength]);

            // 1. создаем структуру мастеров(свойств-объектов данных).
            System.Collections.SortedList assList = new System.Collections.SortedList();
            int index = customizationStructViewPropertiesLength + 1 + advColsLength;
            CreateMastersStruct(dobject, values, ref index, 0, storageStruct.sources, assList, typesByKeys, dataObjectCache);
            assList.Add(storageStruct.sources, new object[] { dobject, 0 });

            // 2. заливаем данные.
            System.Collections.ArrayList properiesValues = new System.Collections.ArrayList();
            StringCollection allAdvCols = new StringCollection();

            int masterPosition = index;

            for (int i = 0; i < advColsLength; i++)
            {
                object value = values[i + customizationStructViewPropertiesLength];
                if (value == DBNull.Value) value = null;
                properiesValues.Add(new[] { advCols[i].Name, value, dobject });
                allAdvCols.Add(advCols[i].Name);
                dobject.DynamicProperties.Add(advCols[i].Name, null);
            }

            for (int i = 0; i < customizationStructViewPropertiesLength; i++)
            {
                StorageStructForView.PropStorage prop = storageStruct.props[i];
                if (Information.IsStoredProperty(dobjectType, prop.Name) || prop.Expression != null)
                {
                    if (prop.MastersTypes == null)
                    {
                        object[] tmp = (object[])assList[prop.source];
                        object value;
                        if (customizationStruct.ColumnsOrder != null && customizationStruct.ColumnsOrder.Length >= customizationStructViewPropertiesLength)
                        {
                            value = values[Array.IndexOf(customizationStruct.ColumnsOrder, prop.Name)];
                        }
                        else
                        {
                            value = values[i];
                        }

                        if (value == DBNull.Value) value = null;
                        if (tmp != null)
                            properiesValues.Add(
                                new[] { prop.simpleName, value, tmp[0] });
                    }
                    else
                    {
                        object[] tmp = (object[])assList[prop.source];
                        if (tmp != null)
                        {
                            // Ищем позицию.
                            int tmp1 = (int)tmp[1];
                            int curMasterPosition = masterPosition;
                            for (int j = 0; j < tmp1; j++)
                                curMasterPosition += prop.MastersTypes[j].Length;
                            int k = 0;
                            object value = values[curMasterPosition];
                            if (value == DBNull.Value) value = null;
                            while (k < prop.MastersTypes[tmp1].Length - 1 && value == null)
                            {
                                k++;
                                value = values[curMasterPosition + k];
                                if (value == DBNull.Value) value = null;
                            }

                            object tmp0 = tmp[0];
                            if (value != null)
                            {
                                if (Information.GetPropValueByName((DataObject)tmp0, prop.simpleName) == null)
                                {
                                    DataObject no = dataObjectCache.CreateDataObject(prop.MastersTypes[tmp1][k], value);
                                    if (no.GetStatus(false) == ObjectStatus.Created)
                                    {
                                        no.SetStatus(ObjectStatus.UnAltered);
                                        no.SetLoadingState(LoadingState.LightLoaded);
                                        no.InitDataCopy(dataObjectCache);
                                    }

                                    value = no;
                                    properiesValues.Add(new[] { prop.simpleName, value, tmp0 });
                                }
                                else
                                {
                                    // changed by fat
                                    properiesValues.Add(new[] { prop.simpleName, Information.GetPropValueByName((DataObject)tmp0, prop.simpleName), tmp0 });
                                }
                            }
                            else
                                properiesValues.Add(new[] { prop.simpleName, null, tmp0 });
                        }

                        masterPosition += prop.MastersTypesCount;
                    }
                }
            }

            // 2.2 Записываем в объекты.
            System.Collections.SortedList curObjProperiesValues = new System.Collections.SortedList();
            while (properiesValues.Count > 0)
            {
                // a. Выбираем для текущего объекта все свойства.
                object[] tmp = (object[])properiesValues[0];
                DataObject curobj = (DataObject)tmp[2];
                dobjectType = curobj.GetType();
                curObjProperiesValues.Clear();

                List<string> loadedPropsColl = curobj.GetLoadedPropertiesList();

                for (int i = properiesValues.Count - 1; i >= 0; i--)
                {
                    tmp = (object[])properiesValues[i];
                    if (tmp[2] == curobj)
                    {
                        object tmp0 = tmp[0];
                        if (!curObjProperiesValues.ContainsKey(tmp0))
                        {
                            curObjProperiesValues.Add(tmp0, tmp[1]);
                            if (!loadedPropsColl.Contains((string)tmp0))
                                loadedPropsColl.Add((string)tmp0);
                        }

                        properiesValues.RemoveAt(i);
                    }
                }

                // b. Раскидываем согласно LoadOrder;
                string[] loadOrder = Information.GetLoadingOrder(dobjectType);
                int loadOrderLength = loadOrder.Length;
                for (int i = 0; i < loadOrderLength; i++)
                {
                    string propName = loadOrder[i];
                    if (curObjProperiesValues.ContainsKey(propName))
                    {
                        Information.SetPropValueByName(curobj, propName, curObjProperiesValues[propName]);
                        curObjProperiesValues.Remove(propName);
                    }
                }

                int curObjPropertiesValuesCount = curObjProperiesValues.Count;
                for (int i = 0; i < curObjPropertiesValuesCount; i++)
                    Information.SetPropValueByName(curobj, (string)curObjProperiesValues.GetKey(i), curObjProperiesValues.GetByIndex(i));

                if (loadedPropsColl.Count >= Information.GetAllPropertyNames(dobjectType).Length)
                    curobj.SetLoadingState(LoadingState.Loaded);
                else
                {
                    curobj.SetLoadingState(LoadingState.LightLoaded);
                    curobj.AddLoadedProperties(loadedPropsColl);
                }

                curobj.SetStatus(ObjectStatus.UnAltered);
            }
        }
    }
}
