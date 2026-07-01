class Event {
  const Event({
    required this.eventId,
    required this.organizerId,
    required this.eventStatusId,
    required this.title,
    required this.startsAt,
    required this.endsAt,
    required this.capacity,
    required this.createdAt,
    this.description,
    this.locationText,
  });

  final String eventId;
  final String organizerId;
  final int eventStatusId;
  final String title;
  final String? description;
  final DateTime startsAt;
  final DateTime endsAt;
  final int capacity;
  final String? locationText;
  final DateTime createdAt;

  bool get isDraft => eventStatusId == 1;
  bool get isPublished => eventStatusId == 2;
  bool get isCanceled => eventStatusId == 3;
  bool get isClosed => eventStatusId == 4;

  String get statusLabel => switch (eventStatusId) {
    1 => 'Draft',
    2 => 'Published',
    3 => 'Canceled',
    4 => 'Closed',
    _ => 'Unknown',
  };

  factory Event.fromJson(Map<String, dynamic> json) => Event(
    eventId: json['eventId'] as String,
    organizerId: json['organizerId'] as String,
    eventStatusId: json['eventStatusId'] as int,
    title: json['title'] as String,
    description: json['description'] as String?,
    startsAt: DateTime.parse(json['startsAt'] as String).toLocal(),
    endsAt: DateTime.parse(json['endsAt'] as String).toLocal(),
    capacity: json['capacity'] as int,
    locationText: json['locationText'] as String?,
    createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
  );

  Map<String, dynamic> toMutationJson() => {
    'title': title,
    'description': description,
    'startsAt': startsAt.toUtc().toIso8601String(),
    'endsAt': endsAt.toUtc().toIso8601String(),
    'capacity': capacity,
    'locationText': locationText,
  };
}
