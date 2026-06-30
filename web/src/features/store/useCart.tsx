import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  addCartItem,
  clearCart as apiClearCart,
  getCart,
  removeCartItem,
  updateCartItem,
  type CartDto,
} from "../../services/storeApi";

/**
 * Lightweight cart context. We deliberately keep state minimal — the server
 * is the source of truth for line totals, VAT, and stock — but we cache the
 * last response so the cart drawer/header badge can stay reactive without
 * a network round-trip on every page change.
 */
interface CartContextValue {
  cart: CartDto | null;
  loading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
  add: (productId: string, quantity?: number) => Promise<void>;
  update: (productId: string, quantity: number) => Promise<void>;
  remove: (productId: string) => Promise<void>;
  clear: () => Promise<void>;
  itemCount: number;
}

const CartContext = createContext<CartContextValue | null>(null);

export function CartProvider({ children }: { children: ReactNode }) {
  const [cart, setCart] = useState<CartDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inFlight = useRef(0);

  const wrap = useCallback(
    async (op: () => Promise<CartDto>) => {
      const id = ++inFlight.current;
      setLoading(true);
      setError(null);
      try {
        const next = await op();
        if (inFlight.current === id) setCart(next);
      } catch (err) {
        if (inFlight.current === id) {
          setError((err as Error).message ?? "Cart update failed.");
        }
      } finally {
        if (inFlight.current === id) setLoading(false);
      }
    },
    [],
  );

  const refresh = useCallback(() => wrap(getCart), [wrap]);
  const add = useCallback(
    (productId: string, quantity = 1) => wrap(() => addCartItem(productId, quantity)),
    [wrap],
  );
  const update = useCallback(
    (productId: string, quantity: number) =>
      wrap(() => updateCartItem(productId, quantity)),
    [wrap],
  );
  const remove = useCallback(
    (productId: string) => wrap(() => removeCartItem(productId)),
    [wrap],
  );
  const clear = useCallback(() => wrap(apiClearCart), [wrap]);

  useEffect(() => {
    refresh().catch(() => undefined);
  }, [refresh]);

  const itemCount = useMemo(
    () => cart?.items.reduce((sum, l) => sum + l.quantity, 0) ?? 0,
    [cart],
  );

  const value = useMemo<CartContextValue>(
    () => ({ cart, loading, error, refresh, add, update, remove, clear, itemCount }),
    [cart, loading, error, refresh, add, update, remove, clear, itemCount],
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartContextValue {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart must be used within a CartProvider.");
  return ctx;
}
