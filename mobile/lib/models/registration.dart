class Registration {
  const Registration({
    required this.registrationId,
    required this.eventId,
    required this.eventTitle,
    required this.registrationStatus,
    required this.registeredAt,
    this.waitlistPosition,
    this.cancelledAt,
  });

  final String registrationId;
  final String eventId;
  final String eventTitle;
  final String registrationStatus;
  final int? waitlistPosition;
  final DateTime registeredAt;
  final DateTime? cancelledAt;

  bool get isActive => cancelledAt == null;

  factory Registration.fromJson(Map<String, dynamic> json) => Registration(
    registrationId: json['registrationId'] as String,
    eventId: json['eventId'] as String,
    eventTitle: json['eventTitle'] as String? ?? '',
    registrationStatus: json['registrationStatus'] as String,
    waitlistPosition: json['waitlistPosition'] as int?,
    registeredAt: DateTime.parse(json['registeredAt'] as String).toLocal(),
    cancelledAt: json['cancelledAt'] == null
        ? null
        : DateTime.parse(json['cancelledAt'] as String).toLocal(),
  );
}

class RosterItem {
  const RosterItem({
    required this.registrationId,
    required this.studentId,
    required this.studentName,
    required this.studentEmail,
    required this.registrationStatus,
    required this.registeredAt,
    this.waitlistPosition,
  });

  final String registrationId;
  final String studentId;
  final String studentName;
  final String studentEmail;
  final String registrationStatus;
  final int? waitlistPosition;
  final DateTime registeredAt;

  factory RosterItem.fromJson(Map<String, dynamic> json) => RosterItem(
    registrationId: json['registrationId'] as String,
    studentId: json['studentId'] as String,
    studentName: json['studentName'] as String,
    studentEmail: json['studentEmail'] as String,
    registrationStatus: json['registrationStatus'] as String,
    waitlistPosition: json['waitlistPosition'] as int?,
    registeredAt: DateTime.parse(json['registeredAt'] as String).toLocal(),
  );
}

class EventRoster {
  const EventRoster({
    required this.eventId,
    required this.capacity,
    required this.confirmedCount,
    required this.waitlistCount,
    required this.confirmed,
    required this.waitlist,
  });

  final String eventId;
  final int capacity;
  final int confirmedCount;
  final int waitlistCount;
  final List<RosterItem> confirmed;
  final List<RosterItem> waitlist;

  factory EventRoster.fromJson(Map<String, dynamic> json) => EventRoster(
    eventId: json['eventId'] as String,
    capacity: json['capacity'] as int,
    confirmedCount: json['confirmedCount'] as int,
    waitlistCount: json['waitlistCount'] as int,
    confirmed: (json['confirmed'] as List<dynamic>)
        .map((item) => RosterItem.fromJson(item as Map<String, dynamic>))
        .toList(),
    waitlist: (json['waitlist'] as List<dynamic>)
        .map((item) => RosterItem.fromJson(item as Map<String, dynamic>))
        .toList(),
  );
}
