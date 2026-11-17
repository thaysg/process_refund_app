import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../state/providers.dart';
import 'view/home_view.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  Future<void> _pickFile(BuildContext context, WidgetRef ref) async {
    final files = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: const ['csv'],
    );
    if (files.isEmpty) return;

    final file = files.single;
    final bytes = await file.readAsBytes();

    ref.read(selectedFileProvider.notifier).state = SelectedFileState(
      name: file.name,
      bytes: bytes,
    );
  }

  Future<void> _pickDate(BuildContext context, WidgetRef ref) async {
    final current = ref.read(referenceDateProvider);
    final picked = await showDatePicker(
      context: context,
      initialDate: current,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
      helpText: 'Selecione a data de referência',
      cancelText: 'Cancelar',
      confirmText: 'OK',
    );
    if (picked != null) {
      ref.read(referenceDateProvider.notifier).state = picked;
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final processingState = ref.watch(processingProvider);
    final selectedFile = ref.watch(selectedFileProvider);
    final referenceDateLabel = ref.watch(referenceDateStringProvider);
    final baseUrl = ref.watch(apiClientProvider).baseUrl;

    return Scaffold(
      appBar: AppBar(
        scrolledUnderElevation: 0,
        title: const Text('Processamento de Reembolsos'),
        centerTitle: false,
        bottom: kDebugMode
            ? PreferredSize(
                preferredSize: const Size.fromHeight(24),
                child: Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Tooltip(
                    message: 'URL base da API (debug): $baseUrl',
                    child: Text(
                      'API: $baseUrl',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ),
                ),
              )
            : null,
      ),
      body: HomeView(
        fileName: selectedFile?.name,
        referenceDateLabel: referenceDateLabel,
        loading: processingState.isLoading,
        onPickFile: () => _pickFile(context, ref),
        onPickDate: () => _pickDate(context, ref),
        onProcess: () => ref.read(processingProvider.notifier).process(),
        onRetry: () => ref.read(processingProvider.notifier).process(),
      ),
    );
  }
}
