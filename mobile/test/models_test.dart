import 'package:flutter_test/flutter_test.dart';
import 'package:process_refund_app/src/domain/models.dart';

void main() {
  group('statusFromApi', () {
    test('mapeia status conhecidos', () {
      expect(statusFromApi('Aprovado'), RequestStatusType.aprovado);
      expect(statusFromApi('Rejeitado'), RequestStatusType.rejeitado);
      expect(statusFromApi('Suspeito de Fraude'), RequestStatusType.suspeitoDeFraude);
    });

    test('retorna null para status desconhecido', () {
      expect(statusFromApi('Desconhecido'), isNull);
      expect(statusFromApi(''), isNull);
    });
  });

  test('ReimbursementItem.fromJson lança FormatException para status desconhecido', () {
    expect(
      () => ReimbursementItem.fromJson(const {
        'id': 1,
        'procedureType': 'Outros',
        'procedureDate': '2024-04-10',
        'amountPaid': 100,
        'reimbursedAmount': 50,
        'clientId': 1,
        'status': 'Invalido',
      }),
      throwsA(isA<FormatException>()),
    );
  });
}
