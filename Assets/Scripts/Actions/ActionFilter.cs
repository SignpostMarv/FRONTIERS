using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Frontiers
{
		public class ActionFilter <T> : MonoBehaviour where T : struct, IConvertible, IComparable, IFormattable
		{
				public T Filter;
				public T FilterExceptions;
				public PassThroughBehavior Behavior = PassThroughBehavior.InterceptByFilter;
				public bool HasFocus = false;

				public virtual void WakeUp()
				{

				}

				public void Awake()
				{
						WakeUp();
				}

				public virtual void Update()
				{
						/*
						if (mActionList.Count == 0) {
								return;
						}

						mUpdating = true;
						var enumerator = mActionList.GetEnumerator();
						while (enumerator.MoveNext()) {
								//foreach (KeyValuePair<T,float> actionPair in mActionList) {
								actionPair = enumerator.Current;
								mListenerCheck = null;
								if (mListeners.TryGetValue(actionPair.Key, out mListenerCheck)) {
										CallListeners(mListenerCheck, actionPair.Key, actionPair.Value);
								}
						}
						mUpdating = false;
						mActionList.Clear();


						if (mActionListDuringUpdate.Count > 0) {
								enumerator = mActionListDuringUpdate.GetEnumerator();
								while (enumerator.MoveNext()) {
										//foreach (KeyValuePair<T,float> updateAction in mActionListDuringUpdate) {
										updateAction = enumerator.Current;
										mActionList.Add(updateAction);
								}

								mActionListDuringUpdate.Clear();
						}*/
				}

				protected KeyValuePair<T,float> updateAction;
				protected KeyValuePair<T,float> actionPair;

				public virtual bool GainFocus()
				{
						HasFocus = true;
						return true;
				}

				public virtual bool LoseFocus()
				{
						HasFocus = false;
						return true;
				}

				public virtual bool ReceiveAction(T action, double timeStamp)
				{
						if (!mSubscribersSet) {
								return false;
						}

						bool passThrough = true;
						bool isSubscribed = mSubscriptionCheck(mSubscribed, action);
						bool isException = mSubscriptionCheck(FilterExceptions, action);
				
						switch (Behavior) {
								case PassThroughBehavior.InterceptByFocus:
										if (!isException && HasFocus) {
												passThrough = false;
										}
										break;
								case PassThroughBehavior.InterceptByFilter:
										if (!isException && isSubscribed) {
												passThrough = false;
										}
										break;

								case PassThroughBehavior.InterceptBySubscription:
										if (!isException && isSubscribed) {
												passThrough = false;
										}
										break;

								case PassThroughBehavior.PassThrough:
										break;
				
								case PassThroughBehavior.InterceptAll:
										//this applies to exceptions!!!
										passThrough = false;
										break;
						}

						if (isSubscribed) {
								mListenerCheck = null;
								if (mListeners.TryGetValue(action, out mListenerCheck)) {
										CallListeners(mListenerCheck, action, timeStamp);
								}
						}
						return passThrough;
				}

				protected void CallListeners(List <ActionListener> listenerList, T action, double timeStamp)
				{
						for (int listenerIndex = listenerList.Count - 1; listenerIndex >= 0; listenerIndex--) {
								ActionListener listener = listenerList[listenerIndex];
								if (listener != null && listener.Target != null) {
										listener(timeStamp);
								} else {
										listenerList.RemoveAt(listenerIndex);
								}
						}
				}

				public virtual void Subscribe(T action, ActionListener listner)
				{
						mListenerCheck = null;
						if (mListeners.TryGetValue(action, out mListenerCheck) == false) {
								mListenerCheck = new List <ActionListener>();
								mListeners.Add(action, mListenerCheck);
						}
						mListenerCheck.Add(listner);
						if (mSubscriptionAdd != null) {
								mSubscriptionAdd(ref mSubscribed, action);
								//take care of queued subscriptions at this point
								while (mQueuedSubscriptions.Count > 0) {
										mSubscriptionAdd(ref mSubscribed, mQueuedSubscriptions.Dequeue());
								}
						} else {
								mQueuedSubscriptions.Enqueue(action);
						}
				}

				public void UnsubscribeAll()
				{
						mSubscribed = mDefault;
				}

				protected List <ActionListener> mListenerCheck;
				protected SubscriptionAdd <T> mSubscriptionAdd;
				protected SubscriptionCheck <T> mSubscriptionCheck;
				protected T mSubscribed;
				protected T mDefault;
				protected HashSet <KeyValuePair <T,float>> mActionList = new HashSet <KeyValuePair <T,float>>();
				protected HashSet <KeyValuePair <T,float>> mActionListDuringUpdate = new HashSet <KeyValuePair <T,float>>();
				protected bool mUpdating = false;
				protected bool mSubscribersSet = false;
				protected Dictionary <T, List <ActionListener>>	mListeners;// = new Dictionary <T, List <ActionListener>>();
				protected Queue <T> mQueuedSubscriptions = new Queue <T>();
		}
}