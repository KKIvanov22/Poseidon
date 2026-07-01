import 'package:flutter/material.dart';

import '../../auth/auth_notifier.dart';
import '../../core/api_endpoints.dart';
import '../../core/api_exception.dart';
import '../../models/event.dart';
import '../../models/registration.dart';
import '../../widgets/app_chrome.dart';
import '../../widgets/empty_state.dart';
import '../../widgets/event_card.dart';

class StudentScreen extends StatefulWidget {
  const StudentScreen({super.key, required this.auth});

  final AuthNotifier auth;

  @override
  State<StudentScreen> createState() => _StudentScreenState();
}

class _StudentScreenState extends State<StudentScreen> {
  var _index = 0;
  var _loading = true;
  var _events = <Event>[];
  var _registrations = <Registration>[];
  var _query = '';
  String? _error;
  String? _busyEventId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final token = widget.auth.token;
    if (token == null) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await Future.wait([
        apiGetEvents(token),
        apiGetMyRegistrations(token),
      ]);
      _events = data[0]
          .map((item) => Event.fromJson(item as Map<String, dynamic>))
          .where((event) => event.isPublished)
          .toList();
      _registrations = data[1]
          .map((item) => Registration.fromJson(item as Map<String, dynamic>))
          .toList();
    } on ApiException catch (error) {
      _error = error.message;
    } catch (_) {
      _error = 'Unable to load events right now.';
    }
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _register(Event event) async {
    final token = widget.auth.token;
    if (token == null) return;
    setState(() => _busyEventId = event.eventId);
    try {
      await apiRegisterForEvent(token, event.eventId);
      await _load();
    } on ApiException catch (error) {
      if (mounted) _showSnack(error.message);
    } finally {
      if (mounted) setState(() => _busyEventId = null);
    }
  }

  Future<void> _cancel(Registration registration) async {
    final token = widget.auth.token;
    if (token == null) return;
    setState(() => _busyEventId = registration.eventId);
    try {
      await apiCancelRegistration(token, registration.registrationId);
      await _load();
    } on ApiException catch (error) {
      if (mounted) _showSnack(error.message);
    } finally {
      if (mounted) setState(() => _busyEventId = null);
    }
  }

  void _showSnack(String message) {
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    return AppChrome(
      auth: widget.auth,
      title: 'Events',
      subtitle: 'Browse and manage your registrations',
      selectedIndex: _index,
      onDestinationSelected: (value) => setState(() => _index = value),
      destinations: const [
        NavigationDestination(
          icon: Icon(Icons.explore_outlined),
          selectedIcon: Icon(Icons.explore_rounded),
          label: 'Browse',
        ),
        NavigationDestination(
          icon: Icon(Icons.bookmark_border_rounded),
          selectedIcon: Icon(Icons.bookmark_rounded),
          label: 'Mine',
        ),
      ],
      child: _index == 0 ? _browseTab() : _registrationsTab(),
    );
  }

  Widget _browseTab() {
    final filtered = _events
        .where(
          (event) =>
              event.title.toLowerCase().contains(_query.toLowerCase()) ||
              (event.locationText ?? '').toLowerCase().contains(
                _query.toLowerCase(),
              ),
        )
        .toList();

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextField(
            onChanged: (value) => setState(() => _query = value),
            decoration: const InputDecoration(
              hintText: 'Search events',
              prefixIcon: Icon(Icons.search_rounded),
            ),
          ),
          const SizedBox(height: 16),
          if (_loading)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(32),
                child: CircularProgressIndicator(),
              ),
            )
          else if (_error != null)
            EmptyState(
              icon: Icons.cloud_off_rounded,
              title: "Couldn't load events",
              message: _error!,
            )
          else if (filtered.isEmpty)
            const EmptyState(
              icon: Icons.event_busy_rounded,
              title: 'No events found',
              message: 'Published events will appear here when they are ready.',
            )
          else
            ...filtered.map(
              (event) => Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: EventCard(event: event, trailing: _eventAction(event)),
              ),
            ),
        ],
      ),
    );
  }

  Widget _registrationsTab() {
    final active = _registrations
        .where((registration) => registration.isActive)
        .toList();
    return RefreshIndicator(
      onRefresh: _load,
      child: active.isEmpty && !_loading
          ? const EmptyState(
              icon: Icons.bookmark_add_outlined,
              title: 'No registrations yet',
              message: 'Events you join will be tracked here.',
            )
          : ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: _loading ? 1 : active.length,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                if (_loading) {
                  return const Center(
                    child: Padding(
                      padding: EdgeInsets.all(32),
                      child: CircularProgressIndicator(),
                    ),
                  );
                }
                final registration = active[index];
                return Card(
                  child: ListTile(
                    title: Text(registration.eventTitle),
                    subtitle: Text(
                      registration.waitlistPosition == null
                          ? registration.registrationStatus
                          : '${registration.registrationStatus} #${registration.waitlistPosition}',
                    ),
                    trailing: IconButton(
                      tooltip: 'Cancel registration',
                      onPressed: _busyEventId == registration.eventId
                          ? null
                          : () => _cancel(registration),
                      icon: const Icon(Icons.close_rounded),
                    ),
                  ),
                );
              },
            ),
    );
  }

  Widget _eventAction(Event event) {
    final registration = _registrations
        .where((item) => item.eventId == event.eventId && item.isActive)
        .firstOrNull;
    final busy = _busyEventId == event.eventId;
    if (registration != null) {
      return OutlinedButton.icon(
        onPressed: busy ? null : () => _cancel(registration),
        icon: const Icon(Icons.bookmark_remove_rounded),
        label: Text(registration.registrationStatus),
      );
    }
    return FilledButton.icon(
      onPressed: busy ? null : () => _register(event),
      icon: busy
          ? const SizedBox.square(
              dimension: 16,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          : const Icon(Icons.add_rounded),
      label: const Text('Register'),
    );
  }
}
