import { act, renderHook, waitFor } from '@testing-library/react-native';
import Purchases, { CustomerInfo } from 'react-native-purchases';

import { PREMIUM_ENTITLEMENT, useSubscription } from '../useSubscription';

const mockedPurchases = Purchases as jest.Mocked<typeof Purchases>;

function customerInfoWith(activeEntitlements: Record<string, unknown>): CustomerInfo {
  return { entitlements: { active: activeEntitlements } } as unknown as CustomerInfo;
}

describe('useSubscription', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('reports subscribed when the premium entitlement is active', async () => {
    mockedPurchases.getCustomerInfo.mockResolvedValue(
      customerInfoWith({ [PREMIUM_ENTITLEMENT]: { identifier: PREMIUM_ENTITLEMENT } })
    );
    const { result } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isSubscribed).toBe(true);
  });

  test('reports not subscribed when no entitlements are active', async () => {
    mockedPurchases.getCustomerInfo.mockResolvedValue(customerInfoWith({}));
    const { result } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isSubscribed).toBe(false);
  });

  test('reports not subscribed when the customer info lookup fails', async () => {
    mockedPurchases.getCustomerInfo.mockRejectedValue(new Error('network'));
    const { result } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isSubscribed).toBe(false);
  });

  test('updates subscription state when the customer info update listener fires', async () => {
    mockedPurchases.getCustomerInfo.mockResolvedValue(customerInfoWith({}));
    let capturedListener: ((info: CustomerInfo) => void) | undefined;
    mockedPurchases.addCustomerInfoUpdateListener.mockImplementation((listener) => {
      capturedListener = listener;
      return { remove: jest.fn() };
    });
    const { result } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isSubscribed).toBe(false);
    await act(async () => {
      capturedListener!(
        customerInfoWith({ [PREMIUM_ENTITLEMENT]: { identifier: PREMIUM_ENTITLEMENT } })
      );
    });
    await waitFor(() => expect(result.current.isSubscribed).toBe(true));
  });

  test('does not update state after unmount when promise resolves', async () => {
    let resolveCustomerInfo!: (info: CustomerInfo) => void;
    mockedPurchases.getCustomerInfo.mockReturnValue(
      new Promise((resolve) => {
        resolveCustomerInfo = resolve;
      })
    );
    const { result, unmount } = renderHook(() => useSubscription());
    unmount();
    await act(async () => {
      resolveCustomerInfo(
        customerInfoWith({ [PREMIUM_ENTITLEMENT]: { identifier: PREMIUM_ENTITLEMENT } })
      );
    });
    expect(result.current.isSubscribed).toBe(false);
    expect(result.current.isLoading).toBe(true);
  });

  test('does not update state after unmount when promise rejects', async () => {
    let rejectCustomerInfo!: (error: Error) => void;
    mockedPurchases.getCustomerInfo.mockReturnValue(
      new Promise((_, reject) => {
        rejectCustomerInfo = reject;
      })
    );
    const { result, unmount } = renderHook(() => useSubscription());
    unmount();
    await act(async () => {
      rejectCustomerInfo(new Error('network'));
    });
    expect(result.current.isSubscribed).toBe(false);
    expect(result.current.isLoading).toBe(true);
  });

  test('does not update state after unmount when listener fires', async () => {
    mockedPurchases.getCustomerInfo.mockResolvedValue(customerInfoWith({}));
    let capturedListener: ((info: CustomerInfo) => void) | undefined;
    mockedPurchases.addCustomerInfoUpdateListener.mockImplementation((listener) => {
      capturedListener = listener;
      return { remove: jest.fn() };
    });
    const { result, unmount } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    unmount();
    await act(async () => {
      capturedListener!(
        customerInfoWith({ [PREMIUM_ENTITLEMENT]: { identifier: PREMIUM_ENTITLEMENT } })
      );
    });
    expect(result.current.isSubscribed).toBe(false);
  });
});
