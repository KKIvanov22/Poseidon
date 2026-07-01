class NotificationJob {
  const NotificationJob({
    required this.notificationJobId,
    required this.eventId,
    required this.recipientUserId,
    required this.channel,
    required this.title,
    required this.message,
    required this.payloadJson,
    required this.attempts,
    required this.availableAt,
    required this.createdAt,
  });

  final String notificationJobId;
  final String eventId;
  final String recipientUserId;
  final String channel;
  final String title;
  final String message;
  final String payloadJson;
  final int attempts;
  final DateTime availableAt;
  final DateTime createdAt;

  factory NotificationJob.fromJson(Map<String, dynamic> json) =>
      NotificationJob(
        notificationJobId: json['notificationJobId'] as String,
        eventId: json['eventId'] as String,
        recipientUserId: json['recipientUserId'] as String,
        channel: json['channel'] as String,
        title: json['title'] as String,
        message: json['message'] as String,
        payloadJson: json['payloadJson'] as String,
        attempts: json['attempts'] as int,
        availableAt: DateTime.parse(json['availableAt'] as String).toLocal(),
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
      );
}
