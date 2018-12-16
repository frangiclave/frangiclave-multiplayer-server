using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Messages;
using UnityEngine;
using UnityEngine.Networking;

public class MultiplayerServer : MonoBehaviour {

	private const short Port = 4987;

	private readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();

	private readonly Dictionary<int, string> _clients = new Dictionary<int, string>();

	private string _statusFilePath;

	private const string StatusFileHeader =
		"<!DOCTYPE html><html lang=\"en\">" +
		"<head>" +
		"<meta charset=\"utf-8\">" +
		"<title>Frangiclave Multiplayer Server Status</title>" +
		"<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css?family=Forum|Lato\">" +
		"<link rel=\"stylesheet\" href=\"frangiclave-multiplayer-server-status.css\">" +
		"</head>" +
		"<body>" +
		"<header><h1>Frangiclave Multiplayer Server Status</h1></header>" +
		"<div id=\"wrapper\">" +
		"<p>The following rooms are currently active:</p>" +
		"<ul>";

	private const string StatusFileFooter =  "</div></ul></body></html>";

	private readonly Regex _channelRegex = new Regex(@"^[\w-]{1,10}$", RegexOptions.None);

	private void Start()
	{
		// Prepare the status file and its stylesheet
		TextAsset styleText = Resources.Load<TextAsset>("frangiclave-multiplayer-server-status-style");
		_statusFilePath = Path.Combine(Application.persistentDataPath, "frangiclave-multiplayer-server-status.html");
		File.WriteAllText(
			Path.Combine(Application.persistentDataPath, "frangiclave-multiplayer-server-status.css"), styleText.text);
		WriteStatusToFile();

		Debug.Log("Starting server");
		NetworkServer.Listen(Port);
		Debug.Log("Listening on port " + Port);
		NetworkServer.RegisterHandler(MsgType.Connect, OnConnected);
		NetworkServer.RegisterHandler(MsgType.Disconnect, OnDisconnected);
		NetworkServer.RegisterHandler(NoonMsgType.Situation, OnSituationSent);
		NetworkServer.RegisterHandler(NoonMsgType.RoomEnter, OnRoomEnter);
		Debug.Log("Ready");
	}

	private void BroadcastMessageToRoom(NetworkConnection sourceClient, short messageId, MessageBase message)
	{
		BroadcastMessageToRoom(sourceClient.connectionId, messageId, message);
	}

	private void BroadcastMessageToRoom(int sourceClientId, short messageId, MessageBase message)
	{
		if (!_clients.ContainsKey(sourceClientId))
			return;
		string roomId = _clients[sourceClientId];
		if (roomId == null)
			return;
		Room room = _rooms[roomId];

		foreach (int clientId in room.ClientIds)
		{
			if (clientId == sourceClientId)
				continue;
			NetworkServer.SendToClient(clientId, messageId, message);
		}
	}

	private void OnConnected(NetworkMessage message)
	{
		Debug.Log("Connected to client '" + message.conn.connectionId + "'");
		int clientId = message.conn.connectionId;
		_clients[clientId] = null;
	}

	private void OnDisconnected(NetworkMessage message)
	{
		Debug.Log("Disconnected from client '" + message.conn.connectionId + "'");
		int clientId = message.conn.connectionId;
		RemoveClient(clientId);
	}

	private void OnSituationSent(NetworkMessage message)
	{
		var situationMessage = message.ReadMessage<SituationMessage>();
		Debug.Log(
			"New situation sent: verb '" + situationMessage.VerbId +
			"' with recipe '" + situationMessage.RecipeId + "'");
		BroadcastMessageToRoom(message.conn, NoonMsgType.Situation, situationMessage);
	}

	private void OnRoomEnter(NetworkMessage message)
	{
		var roomEnterMessage = message.ReadMessage<RoomEnterMessage>();
		int clientId = message.conn.connectionId;

		// Remove the client from any existing room it is in
		RemoveClient(clientId);

		// Add the client to its new room
		AddClientToRoom(clientId, roomEnterMessage.RoomId);
	}

	private void AddClientToRoom(int clientId, string roomId)
	{
		// Validate the room name (must be only letters, numbers, dashes and underscores, and no more than 10 characters
		// long)
		bool success = _channelRegex.Match(roomId).Success;
		if (success)
		{
			if (!_rooms.ContainsKey(roomId))
				_rooms[roomId] = new Room(roomId);
			success = _rooms[roomId].AddClient(clientId);
		}
		var roomJoin = new RoomJoinMessage {Success = success};
		NetworkServer.SendToClient(clientId, NoonMsgType.RoomJoin, roomJoin);

		// Update the room status
		WriteStatusToFile();

		// Notify all partners if the room was successfully joined
		if (!success)
			return;
		_clients[clientId] = roomId;
		var partnerJoin = new PartnerJoinMessage();
		BroadcastMessageToRoom(clientId, NoonMsgType.PartnerJoin, partnerJoin);
	}

	private void RemoveClient(int clientId)
	{
		// Remove the client from the server's containers
		if (!_clients.ContainsKey(clientId))
			return;
		string roomId = _clients[clientId];
		if (roomId == null || !_rooms.ContainsKey(roomId))
			return;

		// Notify the client's partners, if any
		var partnerLeave = new PartnerLeaveMessage();
		BroadcastMessageToRoom(clientId, NoonMsgType.PartnerLeave, partnerLeave);
		_rooms[roomId].RemoveClient(clientId);

		// Delete the room if it is empty
		if (_rooms[roomId].NumClients == 0)
			_rooms.Remove(roomId);

		// Update the room status
		WriteStatusToFile();
	}

	private void WriteStatusToFile()
	{
		string body = StatusFileHeader;
		foreach (var room in _rooms.Values)
		{
			bool isFull = room.NumClients == Room.MaxClients;
			body += "<li>";
			if (isFull)
				body += "<em>";
			body += room.Id + " (" + room.NumClients + ")";
			if (isFull)
				body += "</em>";
			body += "</li>";
		}

		body += StatusFileFooter;
		File.WriteAllText(_statusFilePath, body);
	}
}
