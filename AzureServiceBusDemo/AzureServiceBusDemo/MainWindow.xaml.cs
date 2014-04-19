using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace AzureServiceBusDemo
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private const string TopicName = "news";
		private static TopicClient _topicClient;
		static string _connectionString;

		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 접속 시작
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void ButtonConnect_OnClick(object sender, RoutedEventArgs e)
		{
			_connectionString = TextBoxConnectionString.Text;
			if (String.IsNullOrEmpty(_connectionString))
			{
				MessageBox.Show("Empty Connection String");
				return;
			}

			var namespaceManager = NamespaceManager.CreateFromConnectionString(_connectionString);

			// 필터 생성.
			creatUsernameSubscription(namespaceManager, "gildong");
			creatUsernameSubscription(namespaceManager, "youngjae");

			// 수신 프로세스 실행
			var listener = new MessageListener();
			var threadGildong = new Thread(() => listener.ReceiveNewsMessages(ListBoxGildong, "gildong"));
			var threadYoungjae = new Thread(() => listener.ReceiveNewsMessages(ListBoxYoungjae, "youngjae"));

			threadYoungjae.Start();
			threadGildong.Start();

			TextBlockLog.Text = "Listener Ready";
		}


		private void ButtonSend_OnClick(object sender, RoutedEventArgs e)
		{
			if (CheckBoxGildong.IsChecked.GetValueOrDefault(false))
				SendMessages("gildong", TextBoxMessage.Text);
			if (CheckBoxYoungjae.IsChecked.GetValueOrDefault(false))
				SendMessages("youngjae", TextBoxMessage.Text);
		}

		/// <summary>
		/// 필터 생성
		/// </summary>
		/// <param name="namespaceManager"></param>
		/// <param name="username"></param>
		private void creatUsernameSubscription(NamespaceManager namespaceManager, string username)
		{
			if (namespaceManager.SubscriptionExists(TopicName, username))
			{
				namespaceManager.DeleteSubscription(TopicName, username);
			}
			// Create a "HighMessages" filtered subscription
			var filter = new SqlFilter("target LIKE '" + username + "'");

			namespaceManager.CreateSubscription(TopicName, username, filter);
		}

		/// <summary>
		/// 메시지 전송
		/// </summary>
		/// <param name="username"></param>
		/// <param name="text"></param>
		private void SendMessages(string username, string text)
		{
			_topicClient = TopicClient.CreateFromConnectionString(_connectionString, TopicName);

			var m = new BrokeredMessage(text)
			{
				MessageId = DateTime.UtcNow.ToBinary().ToString(),
			};
			m.Properties["target"] = username;

			try
			{
				_topicClient.Send(m);
				var log = String.Format("Message sent to {0}: Id = {1}, Body = {2}", username, m.MessageId, m.GetBody<string>());
				Console.WriteLine(log);
				TextBlockLog.Text = log;
			}
			catch (MessagingException e)
			{
				Console.WriteLine(e.Message);
				throw;
			}

			_topicClient.Close();
		}


		/// <summary>
		/// 수신자. 스레드로 실행됨.
		/// </summary>
		public class MessageListener
		{
			public void ReceiveNewsMessages(ListBox listBox, string username)
			{
				// For PeekLock mode (default) where applications require "at least once" delivery of messages 
				var subscriptionClient =
					SubscriptionClient.CreateFromConnectionString(_connectionString, TopicName, username);
				while (true)
				{
					try
					{
						BrokeredMessage message = subscriptionClient.Receive(TimeSpan.FromSeconds(1));
						if (message != null)
						{
							// 각 명단에 대한 리스트에 삽입.
							var result = String.Format("{1} ({0})", message.MessageId,
								message.GetBody<string>());
							if (listBox != null)
								listBox.Dispatcher.BeginInvoke(new Action(() => listBox.Items.Add(result)));

							// Further custom message processing could go here...
							message.Complete();
						}
						else
						{
							//no more messages in the subscription
							Thread.Sleep(100);
						}
					}
					catch (MessagingException e)
					{
						if (!e.IsTransient)
						{
							Console.WriteLine(e.Message);
							throw;
						}
						HandleTransientErrors(e);
					}
				}
			}

			private static void HandleTransientErrors(MessagingException e)
			{
				//If transient error/exception, let's back-off for 2 seconds and retry
				Console.WriteLine(e.Message);
				Console.WriteLine("Will retry sending the message in 2 seconds");
				Thread.Sleep(2000);
			}
		}
	}

}
