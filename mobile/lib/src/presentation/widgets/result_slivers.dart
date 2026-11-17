import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/api_client.dart';
import '../../state/providers.dart';
import 'empty_state.dart';
import 'error_state.dart';
import 'processing_state.dart';
import 'results_sliver_list.dart';
import 'status_filter.dart';
import 'summary_header.dart';

List<Widget> buildResultSlivers(
  BuildContext context,
  WidgetRef ref, {
  required VoidCallback onRetry,
}) {
  final processingState = ref.watch(processingProvider);
  final filteredItems = ref.watch(filteredItemsProvider);
  final filter = ref.watch(statusFilterProvider);

  return processingState.when(
    data: (data) {
      if (data == null) {
        return const [
          SliverFillRemaining(
            hasScrollBody: false,
            child: EmptyState(
              message:
                  'Selecione um arquivo CSV e toque em Processar para ver os resultados.',
              icon: Icons.description_outlined,
            ),
          ),
        ];
      }
      return [
        SliverToBoxAdapter(
          child: SummaryHeader(
            summary: data.summary,
            referenceDate: data.referenceDate,
          ),
        ),
        const SliverToBoxAdapter(child: SizedBox(height: 12)),
        SliverToBoxAdapter(
          child: StatusFilter(
            selected: filter,
            onSelected: (s) => ref.read(statusFilterProvider.notifier).state = s,
          ),
        ),
        const SliverToBoxAdapter(child: SizedBox(height: 12)),
        if (filteredItems.isEmpty)
          const SliverFillRemaining(
            hasScrollBody: false,
            child: EmptyState(
              message: 'Nenhum resultado para o filtro selecionado.',
              icon: Icons.filter_alt_off_outlined,
            ),
          )
        else
          ResultsSliverList(items: filteredItems),
      ];
    },
    loading: () => const [
      SliverFillRemaining(
        hasScrollBody: false,
        child: ProcessingStateWidget(),
      ),
    ],
    error: (err, _) => [
      SliverFillRemaining(
        hasScrollBody: false,
        child: ErrorState(
          message: _friendlyError(err, ref),
          onRetry: onRetry,
        ),
      ),
    ],
  );
}

String _friendlyError(Object err, WidgetRef ref) {
  if (err is ApiException) return err.message;
  final msg = err.toString();
  final baseUrl = ref.read(apiClientProvider).baseUrl;
  if (msg.contains('SocketException') ||
      msg.contains('ClientException') ||
      msg.contains('Connection')) {
    if (kDebugMode) {
      return 'Não foi possível conectar à API. Verifique se o servidor está em execução e se a URL base da API está correta. (URL base: $baseUrl)';
    }
    return 'Não foi possível conectar à API. Verifique se o servidor está em execução e se a URL base da API está correta.';
  }
  if (kDebugMode) {
    return 'Ocorreu um erro ao processar o arquivo. Tente novamente. (URL base: $baseUrl)';
  }
  return 'Ocorreu um erro ao processar o arquivo. Tente novamente.';
}
