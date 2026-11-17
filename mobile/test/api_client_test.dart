import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:process_refund_app/src/data/api_client.dart';

void main() {
  group('ReimbursementApiClient', () {
    test('processCsv faz parsing de resposta bem-sucedida', () async {
      final mock = MockClient((request) async {
        expect(request.method, 'POST');
        expect(request.url.path, '/api/reimbursements/process');
        final body = jsonEncode({
          'referenceDate': '2024-05-01',
          'summary': {
            'total': 1,
            'approved': 1,
            'rejected': 0,
            'suspectedFraud': 0,
            'totalReimbursed': 320
          },
          'items': [
            {
              'id': 1,
              'procedureType': 'Consulta Médica',
              'procedureDate': '2024-02-10',
              'amountPaid': 400,
              'reimbursedAmount': 320,
              'clientId': 123,
              'status': 'Aprovado',
              'reason': 'Aprovado',
              'reasonCode': 'Approved'
            }
          ]
        });
        return http.Response(body, 200,
            headers: {'content-type': 'application/json'});
      });

      final client = ReimbursementApiClient(
          httpClient: mock, baseUrl: 'http://localhost:5000');
      final response = await client.processCsv(
        fileBytes: Uint8List.fromList(utf8.encode('csv')),
        fileName: 'sample.csv',
        referenceDate: '2024-05-01',
      );

      expect(response.referenceDate, '2024-05-01');
      expect(response.summary.total, 1);
      expect(response.items, hasLength(1));
      expect(response.items.first.status, 'Aprovado');
      expect(response.items.first.reasonCode, 'Approved');
    });

    test('processCsv mapeia erros 400 com array de validação', () async {
      final mock = MockClient((request) async {
        final body = jsonEncode({
          'title': 'CSV validation error',
          'status': 400,
          'errors': [
            {'line': 2, 'field': 'Id', 'message': 'Invalid Id'},
            {'line': 3, 'field': 'ValorPago', 'message': 'Invalid amount'},
          ]
        });
        return http.Response(body, 400,
            headers: {'content-type': 'application/problem+json'});
      });

      final client = ReimbursementApiClient(
          httpClient: mock, baseUrl: 'http://localhost:5000');

      await expectLater(
        client.processCsv(
          fileBytes: Uint8List.fromList(utf8.encode('csv')),
          fileName: 'sample.csv',
          referenceDate: '2024-05-01',
        ),
        throwsA(isA<ApiException>().having(
          (e) => e.message,
          'message',
          contains('Linha 2 [Id]: Invalid Id'),
        )),
      );
    });

    test('processCsv mapeia erro de conexão', () async {
      final mock = MockClient((request) async {
        throw http.ClientException('failed to connect');
      });

      final client = ReimbursementApiClient(
          httpClient: mock, baseUrl: 'http://localhost:5000');

      await expectLater(
        client.processCsv(
          fileBytes: Uint8List.fromList(utf8.encode('csv')),
          fileName: 'sample.csv',
          referenceDate: '2024-05-01',
        ),
        throwsA(isA<ApiException>().having(
          (e) => e.message,
          'message',
          contains('Não foi possível conectar à API'),
        )),
      );
    });

    test('processCsv lança mensagem de timeout quando a API não responde', () async {
      final mock = MockClient((request) async {
        return Completer<http.Response>().future;
      });

      final client = ReimbursementApiClient(
        httpClient: mock,
        baseUrl: 'http://localhost:5000',
        timeout: const Duration(milliseconds: 10),
      );

      await expectLater(
        client.processCsv(
          fileBytes: Uint8List.fromList(utf8.encode('csv')),
          fileName: 'sample.csv',
          referenceDate: '2024-05-01',
        ),
        throwsA(isA<ApiException>().having(
          (e) => e.message,
          'message',
          'A API demorou demais para responder. Verifique a conexão e a URL base.',
        )),
      );
    });
  });
}
