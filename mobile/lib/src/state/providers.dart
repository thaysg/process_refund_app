import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:intl/intl.dart';

import '../data/api_client.dart';
import '../domain/models.dart';

final apiClientProvider = Provider<ReimbursementApiClient>((ref) {
  return ReimbursementApiClient();
});

class SelectedFileState {
  final String name;
  final Uint8List bytes;

  const SelectedFileState({required this.name, required this.bytes});
}

final selectedFileProvider = StateProvider<SelectedFileState?>((ref) => null);

final referenceDateProvider = StateProvider<DateTime>((ref) => DateTime.now());

final referenceDateStringProvider = Provider<String>((ref) {
  final date = ref.watch(referenceDateProvider);
  return DateFormat('yyyy-MM-dd').format(date);
});

class ProcessingNotifier extends AsyncNotifier<ProcessingResponse?> {
  @override
  Future<ProcessingResponse?> build() async => null;

  Future<void> process() async {
    final file = ref.read(selectedFileProvider);
    if (file == null) {
      state = AsyncError(
          ApiException('Selecione um arquivo CSV antes de processar.'),
          StackTrace.current);
      return;
    }

    final referenceDate = ref.read(referenceDateStringProvider);
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final client = ref.read(apiClientProvider);
      return client.processCsv(
        fileBytes: file.bytes,
        fileName: file.name,
        referenceDate: referenceDate,
      );
    });

    if (state.hasValue) {
      ref.read(statusFilterProvider.notifier).state = null;
    }
  }

  void reset() {
    state = const AsyncData(null);
  }
}

final processingProvider =
    AsyncNotifierProvider<ProcessingNotifier, ProcessingResponse?>(
  ProcessingNotifier.new,
);

final statusFilterProvider = StateProvider<RequestStatusType?>((ref) => null);

final filteredItemsProvider = Provider<List<ReimbursementItem>>((ref) {
  final filter = ref.watch(statusFilterProvider);
  final asyncResult = ref.watch(processingProvider);
  final response = asyncResult.value;
  if (response == null) return const [];
  if (filter == null) return response.items;
  return response.items.where((i) => i.statusType == filter).toList();
});
