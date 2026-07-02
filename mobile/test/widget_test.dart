import 'package:flutter_test/flutter_test.dart';
import 'package:poseidon/models/event.dart';

void main() {
  test('event model parses API payload and exposes status label', () {
    final event = Event.fromJson({
      'eventId': '11111111-1111-1111-1111-111111111111',
      'organizerId': '22222222-2222-2222-2222-222222222222',
      'eventStatusId': 2,
      'title': 'Marine robotics workshop',
      'description': 'Build and test an ROV.',
      'startsAt': '2026-07-10T10:00:00Z',
      'endsAt': '2026-07-10T12:00:00Z',
      'capacity': 24,
      'locationText': 'Lab 3',
      'createdAt': '2026-07-01T10:00:00Z',
    });

    expect(event.statusLabel, 'Published');
    expect(event.isPublished, isTrue);
    expect(event.capacity, 24);
  });
}
