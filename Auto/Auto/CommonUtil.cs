using RoR2;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace AutoPlay
{
    internal static class ProcChainMaskNetworkWriterExtension
    {
        public static void Write(this NetworkWriter writer, ProcChainMask procChainMask)
        {
            writer.Write(procChainMask.mask);
        }
    }

    internal static class DamageInfoNetworkWriterExtension
    {
        public static void Write(this NetworkWriter writer, DamageInfo damageInfo)
        {
            writer.Write(damageInfo.damage);
            writer.Write(damageInfo.crit);
            writer.Write(damageInfo.attacker);
            writer.Write(damageInfo.inflictor);
            writer.Write(damageInfo.position);
            writer.Write(damageInfo.force);
            writer.Write(damageInfo.procChainMask);
            writer.Write(damageInfo.procCoefficient);
            writer.Write((byte)damageInfo.damageType);
            writer.Write((byte)damageInfo.damageColorIndex);
        }
    }

    static class CommonUtil
    {
        public static int pickUpDuring = 5;
        public static object InvokeNonPublicMethod(object instance, string methodName, object[] param)
        {
            Type type = instance.GetType();
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            object result;
            try
            {
                result = method.Invoke(instance, param);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
            return result;
        }
        
        public static object SetNotPublicPro(object instance, string varName, object newVar)
        {
            Type type = instance.GetType();
            PropertyInfo field = type.GetProperty(varName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            field.SetValue(instance, newVar);
            return field.GetValue(instance);
        }

        public static object GetNotPublicPro(object instance, string varName)
        {
            Type type = instance.GetType();
            PropertyInfo field = type.GetProperty(varName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);            
            return field.GetValue(instance);
        }
        
        public static object GetNotPublicVar(object instance, string varName)
        {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(varName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field.GetValue(instance);
        }

        public static object SetNotPublicVar(object instance, string varName, object newVar)
        {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(varName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            field.SetValue(instance, newVar);
            return field.GetValue(instance);
        }


        public static GameObject CreateGameObject()
        {
            GameObject gameObject = null;
            if (NetworkServer.active)
            {
                ClassicStageInfo component = SceneInfo.instance.GetComponent<ClassicStageInfo>();
                DirectorCard directorCard = CommonUtil.SelectCard(component.interactableSelection, 100);
                while (!directorCard.spawnCard.name.Contains("Barrel"))
                {
                    directorCard = CommonUtil.SelectCard(component.interactableSelection, 100);
                }

                gameObject = directorCard.spawnCard.DoSpawn(Vector3.zero, Quaternion.identity);
            }
            return gameObject;
        }

        private static DirectorCard SelectCard(WeightedSelection<DirectorCard> deck, int maxCost)
        {
            WeightedSelection<DirectorCard> cardSelector = new WeightedSelection<DirectorCard>(8);
            cardSelector.Clear();
            int i = 0;
            int count = deck.Count;
            while (i < count)
            {
                WeightedSelection<DirectorCard>.ChoiceInfo choice = deck.GetChoice(i);
                if (choice.value.cost <= maxCost)
                {
                    cardSelector.AddChoice(choice);
                }
                i++;
            }
            if (cardSelector.Count == 0)
            {
                return null;
            }
            return cardSelector.Evaluate(new Xoroshiro128Plus((ulong)Run.instance.stageRng.nextUint).nextNormalizedFloat);
        }

    }
}
