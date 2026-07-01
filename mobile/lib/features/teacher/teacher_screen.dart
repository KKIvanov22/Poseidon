import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../auth/auth_notifier.dart';
import '../../core/api_endpoints.dart';
import '../../core/api_exception.dart';
import '../../models/event.dart';
import '../../models/registration.dart';
import '../../widgets/app_chrome.dart';
import '../../widgets/empty_state.dart';
import '../../widgets/event_card.dart';

class TeacherScreen extends StatefulWidget {
  const TeacherScreen({super.key, required this.auth});

  final AuthNotifier auth;

  @override
  State<TeacherScreen> createState() => _TeacherScreenState();
}

class _TeacherScreenState extends State<TeacherScreen> {
  var _events = <Event>[];
  var _loading = true;
  var _filter = 0;
  String? _error;

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
      _events = (await apiGetMyEvents(
        token,
      )).map((item) => Event.fromJson(item as Map<String, dynamic>)).toList();
    } on ApiException catch (error) {
      _error = error.message;
    } catch (_) {
      _error = 'Unable to load your events.';
    }
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _mutate(Future<void> Function(String token) action) async {
    final token = widget.auth.token;
    if (token == null) return;
    try {
      await action(token);
      await _load();
    } on ApiException catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.message)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final visible = _filter == 0
        ? _events
        : _events.where((event) => event.eventStatusId == _filter).toList();
    return AppChrome(
      auth: widget.auth,
      title: 'Teacher',
      subtitle: 'Create, publish, and monitor events',
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _showEventSheet(),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Event'),
      ),
      child: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  _chip('All', 0),
                  _chip('Draft', 1),
                  _chip('Published', 2),
                  _chip('Canceled', 3),
                  _chip('Closed', 4),
                ],
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
            else if (visible.isEmpty)
              const EmptyState(
                icon: Icons.event_note_rounded,
                title: 'No events here',
                message: 'Create an event with the action button.',
              )
            else
              ...visible.map(
                (event) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: EventCard(
                    event: event,
                    onTap: () => _showActions(event),
                    trailing: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        OutlinedButton.icon(
                          onPressed: () => _showRoster(event),
                          icon: const Icon(Icons.groups_rounded),
                          label: const Text('Roster'),
                        ),
                        if (event.isDraft)
                          FilledButton.icon(
                            onPressed: () => _mutate(
                              (token) async =>
                                  apiPublishEvent(token, event.eventId),
                            ),
                            icon: const Icon(Icons.publish_rounded),
                            label: const Text('Publish'),
                          ),
                      ],
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _chip(String label, int value) {
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: ChoiceChip(
        label: Text(label),
        selected: _filter == value,
        onSelected: (_) => setState(() => _filter = value),
      ),
    );
  }

  void _showActions(Event event) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.edit_rounded),
              title: const Text('Edit details'),
              onTap: () {
                Navigator.pop(context);
                _showEventSheet(event: event);
              },
            ),
            ListTile(
              leading: const Icon(Icons.groups_rounded),
              title: const Text('View roster'),
              onTap: () {
                Navigator.pop(context);
                _showRoster(event);
              },
            ),
            if (!event.isCanceled)
              ListTile(
                leading: const Icon(Icons.cancel_rounded),
                title: const Text('Cancel event'),
                onTap: () {
                  Navigator.pop(context);
                  _mutate(
                    (token) async => apiCancelEvent(token, event.eventId),
                  );
                },
              ),
          ],
        ),
      ),
    );
  }

  Future<void> _showEventSheet({Event? event}) async {
    final token = widget.auth.token;
    if (token == null) return;
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) => EventFormSheet(token: token, event: event),
    );
    if (saved == true) await _load();
  }

  Future<void> _showRoster(Event event) async {
    final token = widget.auth.token;
    if (token == null) return;
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) => RosterSheet(token: token, event: event),
    );
  }
}

class EventFormSheet extends StatefulWidget {
  const EventFormSheet({super.key, required this.token, this.event});

  final String token;
  final Event? event;

  @override
  State<EventFormSheet> createState() => _EventFormSheetState();
}

class _EventFormSheetState extends State<EventFormSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _description;
  late final TextEditingController _location;
  late final TextEditingController _capacity;
  late DateTime _startsAt;
  late DateTime _endsAt;
  var _saving = false;

  @override
  void initState() {
    super.initState();
    final event = widget.event;
    _title = TextEditingController(text: event?.title ?? '');
    _description = TextEditingController(text: event?.description ?? '');
    _location = TextEditingController(text: event?.locationText ?? '');
    _capacity = TextEditingController(text: '${event?.capacity ?? 25}');
    _startsAt = event?.startsAt ?? DateTime.now().add(const Duration(days: 1));
    _endsAt = event?.endsAt ?? _startsAt.add(const Duration(hours: 1));
  }

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    _location.dispose();
    _capacity.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    final body = {
      'title': _title.text.trim(),
      'description': _description.text.trim().isEmpty
          ? null
          : _description.text.trim(),
      'startsAt': _startsAt.toUtc().toIso8601String(),
      'endsAt': _endsAt.toUtc().toIso8601String(),
      'capacity': int.parse(_capacity.text),
      'locationText': _location.text.trim().isEmpty
          ? null
          : _location.text.trim(),
    };
    try {
      if (widget.event == null) {
        await apiCreateEvent(widget.token, body);
      } else {
        await apiUpdateEvent(widget.token, widget.event!.eventId, body);
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.message)));
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.of(context).viewInsets.bottom;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 0, 16, bottom + 16),
      child: Form(
        key: _formKey,
        child: ListView(
          shrinkWrap: true,
          children: [
            Text(
              widget.event == null ? 'New event' : 'Edit event',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _title,
              decoration: const InputDecoration(labelText: 'Title'),
              validator: (value) =>
                  (value ?? '').trim().isEmpty ? 'Title is required.' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _description,
              maxLines: 3,
              decoration: const InputDecoration(labelText: 'Description'),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _location,
              decoration: const InputDecoration(labelText: 'Location'),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _capacity,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Capacity'),
              validator: (value) => (int.tryParse(value ?? '') ?? 0) > 0
                  ? null
                  : 'Enter a capacity.',
            ),
            const SizedBox(height: 12),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.event_rounded),
              title: const Text('Starts'),
              subtitle: Text(DateFormat('MMM d, yyyy HH:mm').format(_startsAt)),
              onTap: () async {
                final picked = await _pickDateTime(_startsAt);
                if (picked != null) setState(() => _startsAt = picked);
              },
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.event_available_rounded),
              title: const Text('Ends'),
              subtitle: Text(DateFormat('MMM d, yyyy HH:mm').format(_endsAt)),
              onTap: () async {
                final picked = await _pickDateTime(_endsAt);
                if (picked != null) setState(() => _endsAt = picked);
              },
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: _saving ? null : _save,
              icon: _saving
                  ? const SizedBox.square(
                      dimension: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.save_rounded),
              label: const Text('Save event'),
            ),
          ],
        ),
      ),
    );
  }

  Future<DateTime?> _pickDateTime(DateTime initial) async {
    final date = await showDatePicker(
      context: context,
      firstDate: DateTime.now().subtract(const Duration(days: 1)),
      lastDate: DateTime.now().add(const Duration(days: 730)),
      initialDate: initial,
    );
    if (date == null || !mounted) return null;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
    );
    if (time == null) return null;
    return DateTime(date.year, date.month, date.day, time.hour, time.minute);
  }
}

class RosterSheet extends StatefulWidget {
  const RosterSheet({super.key, required this.token, required this.event});

  final String token;
  final Event event;

  @override
  State<RosterSheet> createState() => _RosterSheetState();
}

class _RosterSheetState extends State<RosterSheet> {
  EventRoster? _roster;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      _roster = EventRoster.fromJson(
        await apiGetEventRegistrations(widget.token, widget.event.eventId),
      );
    } on ApiException catch (error) {
      _error = error.message;
    }
    if (mounted) setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final roster = _roster;
    return SizedBox(
      height: MediaQuery.of(context).size.height * 0.82,
      child: DefaultTabController(
        length: 2,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                widget.event.title,
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
            const TabBar(
              tabs: [
                Tab(text: 'Confirmed'),
                Tab(text: 'Waitlist'),
              ],
            ),
            Expanded(
              child: _error != null
                  ? EmptyState(
                      icon: Icons.error_outline_rounded,
                      title: "Couldn't load roster",
                      message: _error!,
                    )
                  : roster == null
                  ? const Center(child: CircularProgressIndicator())
                  : TabBarView(
                      children: [
                        _rosterList(roster.confirmed),
                        _rosterList(roster.waitlist),
                      ],
                    ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _rosterList(List<RosterItem> items) {
    if (items.isEmpty) {
      return const EmptyState(
        icon: Icons.person_off_rounded,
        title: 'No students',
        message: 'Registrations will appear here.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: items.length,
      separatorBuilder: (_, _) => const Divider(height: 1),
      itemBuilder: (context, index) {
        final item = items[index];
        return ListTile(
          leading: CircleAvatar(child: Text('${index + 1}')),
          title: Text(item.studentName),
          subtitle: Text(item.studentEmail),
        );
      },
    );
  }
}
