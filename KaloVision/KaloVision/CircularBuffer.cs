using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaloVision
{
    public class CircularBuffer
    {
        Queue<double> _queue;
        int _size;

        public CircularBuffer(int size)
        {
            _queue = new Queue<double>(size);
            _size = size;
        }

        public void Add(double obj)
        {
            lock(_queue)
            {
                if (_queue.Count == _size)
                {
                    _queue.Dequeue();
                    _queue.Enqueue(obj);
                }
                else
                    _queue.Enqueue(obj);
            }

        }
        public double Read()
        {
            lock (_queue)
            {
                return _queue.Dequeue();
            }
        }

        public double Peek()
        {
            lock (_queue)
            {
                return _queue.Peek();
            }
        }

        public double Avg()
        {
            lock (_queue)
            {
                return _queue.Average();
            }
        }

        public double Avg(int size)
        {
            lock (_queue)
            {
                return _queue.Reverse().Take(size).Average();
            }
        }

        public double Median(int size)
        {
            lock (_queue)
            {
                if (_queue.Count - 1 < (size / 2))
                {
                    return 0.0;
                }

                return _queue.Reverse().Take(size).OrderBy(n => n).ElementAt(size / 2);
            }
        }

        public int Count()
        {
            lock (_queue)
            {
                return _queue.Count;
            }
        }

        public double[] ToArray()
        {
            lock(_queue)
            {
                return _queue.ToArray();
            }
        }
    }
}
