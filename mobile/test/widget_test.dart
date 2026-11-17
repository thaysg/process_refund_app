import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:process_refund_app/src/data/api_client.dart';
import 'package:process_refund_app/src/domain/models.dart';
import 'package:process_refund_app/src/presentation/home_screen.dart';
import 'package:process_refund_app/src/state/providers.dart';

Widget buildTestApp({List<dynamic> overrides = const []}) {
  return ProviderScope(
    overrides: overrides.cast(),
    child: const MaterialApp(
      locale: Locale('pt', 'BR'),
      supportedLocales: [Locale('pt', 'BR')],
      localizationsDelegates: [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: HomeScreen(),
    ),
  );
}

ProcessingResponse sampleResponse() {
  return ProcessingResponse(
    referenceDate: '2024-05-01',
    summary: const ProcessingSummary(
      total: 3,
      approved: 1,
      rejected: 1,
      suspectedFraud: 1,
      totalReimbursed: 320,
    ),
    items: [
      ReimbursementItem.fromJson(const {
        'id': 1,
        'procedureType': 'Consulta Médica',
        'procedureDate': '2024-02-10',
        'amountPaid': 400,
        'reimbursedAmount': 320,
        'clientId': 123,
        'status': 'Aprovado',
        'reason': 'Aprovado',
        'reasonCode': 'Approved',
      }),
      ReimbursementItem.fromJson(const {
        'id': 2,
        'procedureType': 'Outros',
        'procedureDate': '2024-01-20',
        'amountPaid': 150,
        'reimbursedAmount': 75,
        'clientId': 126,
        'status': 'Rejeitado',
        'reason': 'Procedimento fora da janela de 90 dias',
        'reasonCode': 'ProcedureTooOld',
      }),
      ReimbursementItem.fromJson(const {
        'id': 3,
        'procedureType': 'Outros',
        'procedureDate': '2024-04-10',
        'amountPaid': 1000,
        'reimbursedAmount': 500,
        'clientId': 200,
        'status': 'Suspeito de Fraude',
        'reason': 'Limite de solicitações em 30 dias excedido',
        'reasonCode': 'CustomerRequestLimitExceeded',
      }),
    ],
  );
}

Future<void> pumpApp(WidgetTester tester, {List<dynamic> overrides = const []}) async {
  tester.view.physicalSize = const Size(1200, 2400);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(buildTestApp(overrides: overrides));
  await tester.pumpAndSettle();
}

Future<void> pumpWithTextScale(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  tester.platformDispatcher.textScaleFactorTestValue = 2.0;
  addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
  await tester.pumpWidget(buildTestApp(overrides: [
    processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
  ]));
  await tester.pumpAndSettle();
  expect(tester.takeException(), isNull);
}

Future<void> tapChip(WidgetTester tester, String label) async {
  final finder = find.widgetWithText(FilterChip, label);
  await tester.ensureVisible(finder);
  await tester.tap(finder, warnIfMissed: false);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('Tela inicial exibe controles principais', (tester) async {
    await pumpApp(tester);

    expect(find.text('Processamento de Reembolsos'), findsWidgets);
    expect(find.text('Escolher arquivo CSV'), findsOneWidget);
    expect(find.textContaining('Data de referência:'), findsOneWidget);
    expect(find.text('Processar'), findsOneWidget);
  });

  testWidgets('Exibe estado vazio antes do processamento', (tester) async {
    await pumpApp(tester);

    expect(
      find.text(
          'Selecione um arquivo CSV e toque em Processar para ver os resultados.'),
      findsOneWidget,
    );
  });

  testWidgets('Exibe chips de filtro após resultados', (tester) async {
    await pumpApp(tester, overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
    ]);

    expect(find.text('Todos'), findsOneWidget);
    expect(find.text('Aprovado'), findsWidgets);
    expect(find.text('Rejeitado'), findsWidgets);
    expect(find.text('Suspeito de Fraude'), findsWidgets);
    expect(find.textContaining('#1'), findsOneWidget);
  });

  testWidgets('Filtrar por Aprovado mostra apenas aprovados', (tester) async {
    await pumpApp(tester, overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
    ]);

    await tapChip(tester, 'Aprovado');

    expect(find.textContaining('#1'), findsOneWidget);
    expect(find.textContaining('#2'), findsNothing);
    expect(find.textContaining('#3'), findsNothing);
  });

  testWidgets('Filtrar por Rejeitado mostra apenas rejeitados', (tester) async {
    await pumpApp(tester, overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
    ]);

    await tapChip(tester, 'Rejeitado');

    expect(find.textContaining('#2'), findsOneWidget);
    expect(find.textContaining('#1'), findsNothing);
    expect(find.textContaining('#3'), findsNothing);
  });

  testWidgets('Filtrar por Suspeito de Fraude mostra apenas suspeitos', (tester) async {
    await pumpApp(tester, overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
    ]);

    await tapChip(tester, 'Suspeito de Fraude');

    expect(find.textContaining('#3'), findsOneWidget);
    expect(find.textContaining('#1'), findsNothing);
    expect(find.textContaining('#2'), findsNothing);
  });

  testWidgets('Filtro Todos restaura todos os itens', (tester) async {
    await pumpApp(tester, overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
    ]);

    await tapChip(tester, 'Aprovado');
    await tapChip(tester, 'Todos');

    expect(find.textContaining('#1'), findsOneWidget);
    expect(find.textContaining('#2'), findsOneWidget);
    expect(find.textContaining('#3'), findsOneWidget);
  });

  testWidgets('Exibe estado vazio quando filtro não tem resultados', (tester) async {
    final response = ProcessingResponse(
      referenceDate: '2024-05-01',
      summary: const ProcessingSummary(
        total: 1,
        approved: 1,
        rejected: 0,
        suspectedFraud: 0,
        totalReimbursed: 320,
      ),
      items: [
        ReimbursementItem.fromJson(const {
          'id': 1,
          'procedureType': 'Consulta Médica',
          'procedureDate': '2024-02-10',
          'amountPaid': 400,
          'reimbursedAmount': 320,
          'clientId': 123,
          'status': 'Aprovado',
          'reasonCode': 'Approved',
        }),
      ],
    );

    await pumpApp(tester, overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(response)),
    ]);

    await tapChip(tester, 'Rejeitado');

    expect(find.text('Nenhum resultado para o filtro selecionado.'), findsOneWidget);
  });

  testWidgets('Exibe estado de erro e tenta novamente', (tester) async {
    final notifier = _ErrorThenSuccessNotifier();
    await pumpApp(tester, overrides: [
      processingProvider.overrideWith(() => notifier),
      selectedFileProvider.overrideWith((ref) => SelectedFileState(name: 'a.csv', bytes: Uint8List.fromList([1, 2, 3]))),
    ]);

    expect(find.text('Falha simulada'), findsOneWidget);
    expect(find.text('Tentar novamente'), findsOneWidget);

    await tester.tap(find.text('Tentar novamente'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.textContaining('#1'), findsOneWidget);
  });

  testWidgets('Exibe estado de carregamento durante processamento', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(buildTestApp(overrides: [
      processingProvider.overrideWith(() => _LoadingNotifier()),
    ]));
    await tester.pump();

    expect(find.text('Processando reembolsos...'), findsOneWidget);
  });

  testWidgets('Layout sem overflow em 360x640', (tester) async {
    tester.view.physicalSize = const Size(360, 640);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(buildTestApp(overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
    ]));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });

  testWidgets('Layout sem overflow em 640x360 (paisagem)', (tester) async {
    tester.view.physicalSize = const Size(640, 360);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(buildTestApp(overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(sampleResponse())),
    ]));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });

  testWidgets('Layout sem overflow com escala de texto 2.0 em 360x640', (tester) async {
    await pumpWithTextScale(tester, const Size(360, 640));
  });

  testWidgets('Layout sem overflow com escala de texto 2.0 em paisagem', (tester) async {
    await pumpWithTextScale(tester, const Size(640, 360));
  });

  testWidgets('Lista com 500 itens renderiza sem exceções e de forma preguiçosa', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final items = List.generate(500, (i) {
      return ReimbursementItem.fromJson({
        'id': i + 1,
        'procedureType': 'Outros',
        'procedureDate': '2024-04-10',
        'amountPaid': 100,
        'reimbursedAmount': 50,
        'clientId': 1,
        'status': 'Aprovado',
        'reason': 'Aprovado',
        'reasonCode': 'Approved',
      });
    });
    final response = ProcessingResponse(
      referenceDate: '2024-05-01',
      summary: const ProcessingSummary(
        total: 500,
        approved: 500,
        rejected: 0,
        suspectedFraud: 0,
        totalReimbursed: 25000,
      ),
      items: items,
    );

    await tester.pumpWidget(buildTestApp(overrides: [
      processingProvider.overrideWith(() => _FakeProcessingNotifier(response)),
    ]));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.textContaining('#1 •'), findsWidgets);
    final builtTiles = find.byType(Card);
    // Summary card + some visible result tiles, but far fewer than 500 items.
    expect(builtTiles.evaluate().length, lessThan(100));
  });
}

class _FakeProcessingNotifier extends ProcessingNotifier {
  _FakeProcessingNotifier(this._response);
  final ProcessingResponse _response;

  @override
  Future<ProcessingResponse?> build() async => _response;
}

class _ErrorThenSuccessNotifier extends ProcessingNotifier {
  bool _first = true;

  @override
  Future<ProcessingResponse?> build() async => throw ApiException('Falha simulada');

  @override
  Future<void> process() async {
    if (_first) {
      _first = false;
      state = const AsyncLoading();
      state = AsyncData(sampleResponse());
      return;
    }
    state = AsyncData(sampleResponse());
  }
}

class _LoadingNotifier extends ProcessingNotifier {
  @override
  Future<ProcessingResponse?> build() async {
    state = const AsyncLoading();
    return Completer<ProcessingResponse?>().future;
  }
}
